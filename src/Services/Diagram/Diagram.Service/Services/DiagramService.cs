using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Events;
using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Repositories.Interfaces;
using UMLForge.Diagram.Service.DTOs;
using UMLForge.Diagram.Service.Helpers;
using UMLForge.Diagram.Service.Interfaces;
using UMLForge.Diagram.Service.Models.Validation;
using DiagramEntity = UMLForge.Diagram.Repositories.Entities.Diagram;

namespace UMLForge.Diagram.Service.Services;

public class DiagramService : IDiagramService
{
    private const int MaxModelJsonBytes = 256 * 1024; // 256 KB (BR-12)

    private readonly IDiagramRepository _diagramRepository;
    private readonly IDiagramVersionRepository _versionRepository;
    private readonly IMemberAccessRepository _memberAccessRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IOutbox? _outbox;
    private readonly IReviewRepository? _reviewRepository;
    private readonly IDiagramEngineClient _engineClient;

    public DiagramService(
        IDiagramRepository diagramRepository,
        IDiagramVersionRepository versionRepository,
        IMemberAccessRepository memberAccessRepository,
        ICurrentUser currentUser,
        IOutbox? outbox = null,
        IReviewRepository? reviewRepository = null,
        IDiagramEngineClient? engineClient = null)
    {
        _diagramRepository = diagramRepository;
        _versionRepository = versionRepository;
        _memberAccessRepository = memberAccessRepository;
        _currentUser = currentUser;
        _outbox = outbox;
        _reviewRepository = reviewRepository;
        _engineClient = engineClient ?? new FakeDiagramEngineClient();
    }

    public async Task<PagedResult<DiagramSummaryResponse>> GetDiagramsAsync(
        DiagramQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        if (query.Page <= 0) query.Page = 1;
        if (query.PageSize <= 0) query.PageSize = 20;
        if (query.PageSize > 100) query.PageSize = 100;

        var userId = _currentUser.UserId;

        // BR-11: Nếu có ProjectId thì kiểm tra quyền ngay
        if (query.ProjectId.HasValue)
        {
            var hasAccess = await _memberAccessRepository.HasAccessAsync(query.ProjectId.Value, userId, cancellationToken);
            if (!hasAccess)
            {
                throw new BusinessRuleException("BR-11", "You do not have access to this project.", 403);
            }
        }

        var queryable = _diagramRepository.GetQueryable().AsNoTracking();

        if (query.ProjectId.HasValue)
        {
            queryable = queryable.Where(d => d.ProjectId == query.ProjectId.Value);
        }
        else
        {
            var accessibleProjectIds = _memberAccessRepository.GetQueryable()
                .Where(m => m.UserId == userId)
                .Select(m => m.ProjectId);

            queryable = queryable.Where(d => accessibleProjectIds.Contains(d.ProjectId));
        }

        if (query.Type.HasValue)
        {
            queryable = queryable.Where(d => d.Type == query.Type.Value);
        }

        if (query.Status.HasValue)
        {
            queryable = queryable.Where(d => d.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            queryable = queryable.Where(d => d.Title.ToLower().Contains(search));
        }

        queryable = ApplySorting(queryable, query.Sort);

        var totalItems = await queryable.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)query.PageSize);

        var items = await queryable
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(d => new DiagramSummaryResponse
            {
                Id = d.Id,
                ProjectId = d.ProjectId,
                Type = d.Type,
                Title = d.Title,
                Status = d.Status,
                CurrentVersion = d.CurrentVersion,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
                RenderedAt = d.RenderedAt,
                LastError = d.LastError
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<DiagramSummaryResponse>(items, query.Page, query.PageSize, totalItems, totalPages);
    }

    public async Task<(DiagramDetailResponse Detail, string ETag)> GetDiagramByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(id, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{id}' was not found.", 404);
        }

        await EnsureProjectAccessAsync(diagram.ProjectId, allowLecturer: true, cancellationToken);

        var etag = Convert.ToBase64String(diagram.RowVersion);
        var detail = MapToDetail(diagram, etag);
        return (detail, etag);
    }

    public async Task<DiagramDetailResponse> CreateDiagramAsync(
        CreateDiagramRequest request,
        CancellationToken cancellationToken = default)
    {
        // BR-11 & BR-32: Kiểm tra quyền và chặn Lecturer
        await EnsureProjectAccessAsync(request.ProjectId, allowLecturer: false, cancellationToken);

        // BR-12: Kiểm tra ModelJson length <= 256 KB và hợp lệ theo loại sơ đồ
        ValidateModelJson(request.Type, request.ModelJson);

        // BR-18..25: Kiểm tra luật UML qua DiagramEngine
        var umlValidation = await _engineClient.ValidateDiagramAsync(request.Type, request.ModelJson, cancellationToken);
        if (!umlValidation.IsValid)
        {
            var viErrors = umlValidation.Errors.Select(TranslateUmlError).ToArray();
            throw new BusinessRuleException(
                "BR-UML",
                "Mô hình sơ đồ vi phạm quy tắc UML: " + string.Join("; ", viErrors),
                422)
            {
                Errors = new Dictionary<string, string[]> { ["uml"] = viErrors }
            };
        }

        var diagramId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var diagram = new DiagramEntity
        {
            Id = diagramId,
            ProjectId = request.ProjectId,
            Type = request.Type,
            Title = request.Title.Trim(),
            ModelJson = request.ModelJson,
            Status = DiagramStatus.Draft,
            CurrentVersion = 1,
            CreatedBy = _currentUser.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = Array.Empty<byte>()
        };

        // Snapshot phiên bản 1 ban đầu
        var initialVersion = new DiagramVersion
        {
            Id = Guid.NewGuid(),
            DiagramId = diagramId,
            Version = 1,
            Title = diagram.Title,
            ModelJson = diagram.ModelJson,
            CreatedBy = _currentUser.UserId,
            CreatedAt = now
        };

        await _diagramRepository.AddAsync(diagram, cancellationToken);
        await _versionRepository.AddAsync(initialVersion, cancellationToken);
        await _diagramRepository.SaveChangesAsync(cancellationToken);

        var etag = Convert.ToBase64String(diagram.RowVersion);
        return MapToDetail(diagram, etag, warnings: umlValidation.Warnings);
    }

    public async Task<DiagramDetailResponse> UpdateDiagramAsync(
        Guid id,
        UpdateDiagramRequest request,
        string? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(id, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{id}' was not found.", 404);
        }

        // BR-11 & BR-32: Kiểm tra quyền và chặn Lecturer
        await EnsureProjectAccessAsync(diagram.ProjectId, allowLecturer: false, cancellationToken);

        // BR-13: Không sửa khi đang Pending hoặc Rendering
        if (diagram.Status == DiagramStatus.Pending || diagram.Status == DiagramStatus.Rendering)
        {
            throw new BusinessRuleException("BR-13", "Cannot modify diagram while it is pending or rendering.", 409);
        }

        // BR-16: Bắt buộc If-Match header cho concurrency check
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new BusinessRuleException("BR-16", "If-Match header is required for diagram update (concurrency check).", 409);
        }

        var currentEtag = Convert.ToBase64String(diagram.RowVersion);
        var sanitizedIfMatch = ifMatch.Trim('\"');
        if (!string.Equals(currentEtag, sanitizedIfMatch, StringComparison.Ordinal))
        {
            throw new BusinessRuleException("BR-16", "Precondition Failed: Concurrency conflict or ETag mismatch. Diagram has been modified by another process. Please reload.", 409);
        }

        // BR-12: Validate ModelJson
        ValidateModelJson(diagram.Type, request.ModelJson);

        // BR-18..25: Kiểm tra luật UML qua DiagramEngine
        var umlValidation = await _engineClient.ValidateDiagramAsync(diagram.Type, request.ModelJson, cancellationToken);
        if (!umlValidation.IsValid)
        {
            var viErrors = umlValidation.Errors.Select(TranslateUmlError).ToArray();
            throw new BusinessRuleException(
                "BR-UML",
                "Mô hình sơ đồ vi phạm quy tắc UML: " + string.Join("; ", viErrors),
                422)
            {
                Errors = new Dictionary<string, string[]> { ["uml"] = viErrors }
            };
        }

        var now = DateTime.UtcNow;

        // BR-15: Tăng version và ghi lại DiagramVersion trong cùng 1 transaction
        diagram.CurrentVersion++;
        diagram.Title = request.Title.Trim();
        diagram.ModelJson = request.ModelJson;
        diagram.UpdatedAt = now;

        // BR-17: Sửa diagram Rendered hoặc Failed -> quay về Draft, giữ nguyên SvgXml
        if (diagram.Status == DiagramStatus.Rendered || diagram.Status == DiagramStatus.Failed)
        {
            diagram.Status = DiagramStatus.Draft;
        }

        var versionSnapshot = new DiagramVersion
        {
            Id = Guid.NewGuid(),
            DiagramId = diagram.Id,
            Version = diagram.CurrentVersion,
            Title = diagram.Title,
            ModelJson = diagram.ModelJson,
            CreatedBy = _currentUser.UserId,
            CreatedAt = now
        };

        _diagramRepository.Update(diagram);
        await _versionRepository.AddAsync(versionSnapshot, cancellationToken);
        try
        {
            await _diagramRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException(
                "BR-16",
                "Precondition Failed: Concurrency conflict. Diagram has been modified by another process. Please reload.",
                409);
        }

        // BR-31: Khi sửa diagram thuộc project có review mới nhất Approved, trả cờ needsResubmit = true
        var needsResubmit = false;
        if (_reviewRepository != null)
        {
            var latestReview = await _reviewRepository.GetLatestReviewByProjectIdAsync(diagram.ProjectId, cancellationToken);
            if (latestReview != null && latestReview.Status == ReviewStatus.Approved)
            {
                needsResubmit = true;
            }
        }

        var etag = Convert.ToBase64String(diagram.RowVersion);
        return MapToDetail(diagram, etag, needsResubmit, warnings: umlValidation.Warnings);
    }

    public async Task<DiagramValidationResult> ValidateDiagramAsync(
        Guid diagramId,
        string? draftModelJson = null,
        CancellationToken cancellationToken = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(diagramId, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{diagramId}' was not found.", 404);
        }

        await EnsureProjectAccessAsync(diagram.ProjectId, allowLecturer: true, cancellationToken);

        var jsonToValidate = string.IsNullOrWhiteSpace(draftModelJson) ? diagram.ModelJson : draftModelJson;
        ValidateModelJson(diagram.Type, jsonToValidate);

        return await _engineClient.ValidateDiagramAsync(diagram.Type, jsonToValidate, cancellationToken);
    }

    public async Task<IReadOnlyList<RelationSuggestion>> SuggestRelationsAsync(Guid diagramId, CancellationToken cancellationToken = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(diagramId, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{diagramId}' was not found.", 404);
        }

        await EnsureProjectAccessAsync(diagram.ProjectId, allowLecturer: true, cancellationToken);
        return await _engineClient.SuggestRelationsAsync(diagram.Type, diagram.ModelJson, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectDiagramValidationResult>> ValidateProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAccessAsync(projectId, allowLecturer: true, cancellationToken);

        var diagrams = await _diagramRepository.GetQueryable()
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .Select(d => new { d.Id, d.Type, d.ModelJson })
            .ToListAsync(cancellationToken);

        var items = diagrams.Select(d => (d.Id, d.Type, d.ModelJson));
        return await _engineClient.ValidateProjectAsync(items, cancellationToken);
    }

    public async Task DeleteDiagramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(id, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{id}' was not found.", 404);
        }

        await EnsureProjectAccessAsync(diagram.ProjectId, allowLecturer: false, cancellationToken);

        // Xóa mềm
        diagram.IsDeleted = true;
        diagram.UpdatedAt = DateTime.UtcNow;
        _diagramRepository.Update(diagram);
        await _diagramRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<RenderResponse> RequestRenderAsync(Guid diagramId, CancellationToken cancellationToken = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(diagramId, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{diagramId}' was not found.", 404);
        }

        // BR-11 & BR-32: Chỉ thành viên nhóm mới được yêu cầu render (chặn Lecturer)
        await EnsureProjectAccessAsync(diagram.ProjectId, allowLecturer: false, cancellationToken);

        // BR-14: Không render trùng khi đang Pending hoặc Rendering
        if (diagram.Status == DiagramStatus.Pending || diagram.Status == DiagramStatus.Rendering)
        {
            throw new BusinessRuleException("BR-14", "Cannot trigger render while diagram is pending or rendering.", 409);
        }

        // BR-14 (R16): Không render lại cùng version khi đã Rendered hoặc Failed (tránh trùng job worker kẹt Pending theo BR-26)
        if (diagram.Status == DiagramStatus.Rendered || diagram.Status == DiagramStatus.Failed)
        {
            throw new BusinessRuleException("BR-14", "Phiên bản này đã được render hoặc đã thất bại. Để render lại, hãy chỉnh sửa để tạo phiên bản mới hoặc sử dụng chức năng Retry của Admin.", 409);
        }

        // Cập nhật trạng thái sang Pending
        diagram.Status = DiagramStatus.Pending;
        diagram.UpdatedAt = DateTime.UtcNow;
        _diagramRepository.Update(diagram);

        // Tạo sự kiện RenderRequested và lưu vào Outbox trong cùng 1 transaction (T-03: đúng payload chuẩn)
        if (_outbox != null)
        {
            _outbox.Add(StreamNames.Diagram, new RenderRequested(
                diagram.Id,
                diagram.ProjectId,
                diagram.CurrentVersion,
                (int)diagram.Type,
                diagram.Title,
                diagram.ModelJson,
                _currentUser.UserId
            ));
        }

        await _diagramRepository.SaveChangesAsync(cancellationToken);

        return new RenderResponse
        {
            DiagramId = diagram.Id,
            Version = diagram.CurrentVersion,
            Status = diagram.Status,
            Message = "Render request accepted."
        };
    }

    public async Task<PagedResult<DiagramVersionSummaryResponse>> GetVersionsAsync(
        Guid diagramId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var diagram = await _diagramRepository.GetByIdAsync(diagramId, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{diagramId}' was not found.", 404);
        }

        await EnsureProjectAccessAsync(diagram.ProjectId, allowLecturer: true, cancellationToken);

        var totalItems = await _versionRepository.CountVersionsAsync(diagramId, cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var versions = await _versionRepository.GetVersionsPagedAsync(diagramId, page, pageSize, cancellationToken);

        var items = versions.Select(v => new DiagramVersionSummaryResponse
        {
            Id = v.Id,
            DiagramId = v.DiagramId,
            Version = v.Version,
            Title = v.Title,
            CreatedBy = v.CreatedBy,
            CreatedAt = v.CreatedAt
        }).ToList();

        return new PagedResult<DiagramVersionSummaryResponse>(items, page, pageSize, totalItems, totalPages);
    }

    public async Task<DiagramVersionDetailResponse> GetVersionByNumberAsync(
        Guid diagramId,
        int version,
        CancellationToken cancellationToken = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(diagramId, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{diagramId}' was not found.", 404);
        }

        await EnsureProjectAccessAsync(diagram.ProjectId, allowLecturer: true, cancellationToken);

        var versionEntity = await _versionRepository.GetByVersionAsync(diagramId, version, cancellationToken);
        if (versionEntity == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Version '{version}' for diagram '{diagramId}' was not found.", 404);
        }

        return new DiagramVersionDetailResponse
        {
            Id = versionEntity.Id,
            DiagramId = versionEntity.DiagramId,
            Version = versionEntity.Version,
            Title = versionEntity.Title,
            ModelJson = versionEntity.ModelJson,
            CreatedBy = versionEntity.CreatedBy,
            CreatedAt = versionEntity.CreatedAt
        };
    }

    public async Task<DiagramDetailResponse> RestoreVersionAsync(
        Guid diagramId,
        int version,
        CancellationToken cancellationToken = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(diagramId, cancellationToken);
        if (diagram == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Diagram with ID '{diagramId}' was not found.", 404);
        }

        // BR-11 & BR-32: Quyền dự án và chặn Lecturer
        await EnsureProjectAccessAsync(diagram.ProjectId, allowLecturer: false, cancellationToken);

        // BR-13: Không khôi phục/sửa khi đang Pending hoặc Rendering
        if (diagram.Status == DiagramStatus.Pending || diagram.Status == DiagramStatus.Rendering)
        {
            throw new BusinessRuleException("BR-13", "Cannot restore version while diagram is pending or rendering.", 409);
        }

        var versionEntity = await _versionRepository.GetByVersionAsync(diagramId, version, cancellationToken);
        if (versionEntity == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Version '{version}' for diagram '{diagramId}' was not found.", 404);
        }

        var now = DateTime.UtcNow;

        // Tăng CurrentVersion và tạo snapshot phiên bản mới từ bản khôi phục
        diagram.CurrentVersion++;
        diagram.Title = versionEntity.Title;
        diagram.ModelJson = versionEntity.ModelJson;
        diagram.Status = DiagramStatus.Draft; // BR-17
        diagram.UpdatedAt = now;

        var restoredSnapshot = new DiagramVersion
        {
            Id = Guid.NewGuid(),
            DiagramId = diagram.Id,
            Version = diagram.CurrentVersion,
            Title = diagram.Title,
            ModelJson = diagram.ModelJson,
            CreatedBy = _currentUser.UserId,
            CreatedAt = now
        };

        _diagramRepository.Update(diagram);
        await _versionRepository.AddAsync(restoredSnapshot, cancellationToken);
        await _diagramRepository.SaveChangesAsync(cancellationToken);

        // BR-31: Khi khôi phục diagram thuộc project có review mới nhất Approved, trả cờ needsResubmit = true
        var needsResubmit = false;
        if (_reviewRepository != null)
        {
            var latestReview = await _reviewRepository.GetLatestReviewByProjectIdAsync(diagram.ProjectId, cancellationToken);
            if (latestReview != null && latestReview.Status == ReviewStatus.Approved)
            {
                needsResubmit = true;
            }
        }

        var etag = Convert.ToBase64String(diagram.RowVersion);
        return MapToDetail(diagram, etag, needsResubmit);
    }

    private async Task EnsureProjectAccessAsync(Guid projectId, bool allowLecturer, CancellationToken cancellationToken)
    {
        var access = await _memberAccessRepository.GetAccessAsync(projectId, _currentUser.UserId, cancellationToken);
        if (access == null)
        {
            throw new BusinessRuleException("BR-11", "You do not have access to this project.", 403);
        }

        // BR-32: Giảng viên chỉ được xem và bình luận, không sửa hay tạo mới
        if (!allowLecturer && string.Equals(access.Role, ProjectRole.Lecturer, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("BR-32", "Lecturers have read-only access to diagrams.", 403);
        }
    }

    private static void ValidateModelJson(DiagramType type, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new BusinessRuleException("BR-12", "ModelJson is required.", 400);
        }

        var byteCount = Encoding.UTF8.GetByteCount(json);
        if (byteCount > MaxModelJsonBytes)
        {
            throw new BusinessRuleException("BR-12", $"ModelJson size exceeds the maximum allowed limit of {MaxModelJsonBytes} bytes (256 KB).", 400);
        }

        if (!DiagramModelParser.TryParse(type, json, out _, out var error))
        {
            throw new BusinessRuleException("BR-12", $"Invalid model JSON for diagram type '{type}': {error}", 400);
        }
    }

    private static IQueryable<DiagramEntity> ApplySorting(IQueryable<DiagramEntity> queryable, string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return queryable.OrderByDescending(d => d.UpdatedAt);
        }

        var parts = sort.Split(':');
        var field = parts[0].Trim().ToLowerInvariant();
        var isAsc = parts.Length > 1 && string.Equals(parts[1].Trim(), "asc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "title" => isAsc ? queryable.OrderBy(d => d.Title) : queryable.OrderByDescending(d => d.Title),
            "createdat" => isAsc ? queryable.OrderBy(d => d.CreatedAt) : queryable.OrderByDescending(d => d.CreatedAt),
            "type" => isAsc ? queryable.OrderBy(d => d.Type) : queryable.OrderByDescending(d => d.Type),
            "status" => isAsc ? queryable.OrderBy(d => d.Status) : queryable.OrderByDescending(d => d.Status),
            _ => isAsc ? queryable.OrderBy(d => d.UpdatedAt) : queryable.OrderByDescending(d => d.UpdatedAt)
        };
    }

    private static DiagramDetailResponse MapToDetail(
        DiagramEntity diagram,
        string etag,
        bool needsResubmit = false,
        IEnumerable<string>? warnings = null)
    {
        return new DiagramDetailResponse
        {
            Id = diagram.Id,
            ProjectId = diagram.ProjectId,
            Type = diagram.Type,
            Title = diagram.Title,
            Status = diagram.Status,
            CurrentVersion = diagram.CurrentVersion,
            ModelJson = diagram.ModelJson,
            PlantUml = diagram.PlantUml,
            SvgXml = diagram.SvgXml,
            CreatedAt = diagram.CreatedAt,
            UpdatedAt = diagram.UpdatedAt,
            RenderedAt = diagram.RenderedAt,
            LastError = diagram.LastError,
            ETag = etag,
            NeedsResubmit = needsResubmit,
            Warnings = warnings?.ToList() ?? new List<string>()
        };
    }

    private static string TranslateUmlError(string error)
    {
        if (string.IsNullOrWhiteSpace(error)) return error;

        // Business Context
        if (error.Contains("BusinessContext diagram requires a title"))
            return "Sơ đồ Business Context yêu cầu phải có tiêu đề (title).";
        if (error.Contains("BusinessContext diagram requires a primary system boundary name"))
            return "Sơ đồ Business Context yêu cầu phải có tên hệ thống chính (thuộc tính 'system').";
        if (error.Contains("BusinessContext diagram requires at least one external entity"))
            return "Sơ đồ Business Context yêu cầu phải có ít nhất một thực thể bên ngoài (danh sách 'externalSystems').";
        if (error.Contains("Flow source") && error.Contains("is neither the system"))
            return error.Replace("Flow source", "Nguồn luồng dữ liệu").Replace("is neither the system, an internal component nor an external entity.", "không thuộc hệ thống, thành phần nội bộ hay thực thể bên ngoài.");
        if (error.Contains("Flow target") && error.Contains("is neither the system"))
            return error.Replace("Flow target", "Đích luồng dữ liệu").Replace("is neither the system, an internal component nor an external entity.", "không thuộc hệ thống, thành phần nội bộ hay thực thể bên ngoài.");
        if (error.Contains("is between two external entities and is not shown in a context diagram"))
            return error.Replace("Flow", "Luồng dữ liệu").Replace("is between two external entities and is not shown in a context diagram.", "kết nối giữa 2 thực thể bên ngoài (không được phép trong sơ đồ ngữ cảnh).");
        if (error.Contains("has no data flow with the system"))
            return error.Replace("External entity", "Thực thể bên ngoài").Replace("has no data flow with the system.", "chưa có luồng dữ liệu kết nối với hệ thống.");

        // Use Case
        if (error.Contains("UseCase diagram requires a title"))
            return "Sơ đồ Use Case yêu cầu phải có tiêu đề (title).";
        if (error.Contains("UseCase diagram must have at least one actor"))
            return "Sơ đồ Use Case phải có ít nhất một tác nhân (thuộc tính 'actors').";
        if (error.Contains("UseCase diagram must have at least one use case"))
            return "Sơ đồ Use Case phải có ít nhất một ca sử dụng (thuộc tính 'useCases').";
        if (error.Contains("is declared as both an actor and a use case"))
            return error.Replace("Name", "Tên").Replace("is declared as both an actor and a use case.", "đang bị khai báo trùng lặp vừa là Actor vừa là Use Case.");
        if (error.Contains("Relation must have non-empty 'from' and 'to' targets"))
            return "Quan hệ trong sơ đồ phải có thuộc tính 'from' và 'to' không được để trống.";
        if (error.Contains("cannot point to itself"))
            return error.Replace("Relation", "Quan hệ").Replace("cannot point to itself.", "không thể tự trỏ tới chính nó.");
        if (error.Contains("must connect an actor with a use case"))
            return error.Replace("Association", "Quan hệ liên kết").Replace("must connect an actor with a use case. Use 'include', 'extend' or 'generalization' instead.", "phải kết nối giữa một Actor với một Use Case. Sử dụng 'include', 'extend' hoặc 'generalization' nếu giữa 2 use case.");
        if (error.Contains("must connect two use cases"))
            return error.Replace("relation", "quan hệ").Replace("must connect two use cases.", "phải kết nối giữa 2 Use Case.");
        if (error.Contains("must connect two actors or two use cases"))
            return error.Replace("Generalization", "Quan hệ kế thừa").Replace("must connect two actors or two use cases.", "phải kết nối giữa 2 Actor hoặc giữa 2 Use Case.");
        if (error.Contains("relations must not form a cycle"))
            return error.Replace("relations must not form a cycle.", "không được tạo thành vòng lặp chu trình.");

        // Sequence
        if (error.Contains("Sequence diagram requires a title"))
            return "Sơ đồ Sequence yêu cầu phải có tiêu đề (title).";
        if (error.Contains("Sequence diagram must have at least two participants"))
            return "Sơ đồ Sequence phải có ít nhất 2 đối tượng tham gia (thuộc tính 'participants') để gửi nhận thông điệp.";
        if (error.Contains("Sequence diagram must contain at least one message"))
            return "Sơ đồ Sequence phải có ít nhất một thông điệp (thuộc tính 'messages').";
        if (error.Contains("is not in declared participants list"))
            return error.Replace("Sequence message source", "Nguồn thông điệp").Replace("Sequence message target", "Đích thông điệp").Replace("is not in declared participants list.", "chưa được khai báo trong danh sách participants.");

        // Activity
        if (error.Contains("Activity diagram requires a title"))
            return "Sơ đồ Activity yêu cầu phải có tiêu đề (title).";
        if (error.Contains("Activity diagram must contain at least one action step"))
            return "Sơ đồ Activity phải có ít nhất một bước hành động (thuộc tính 'steps').";

        // State
        if (error.Contains("State diagram requires a title"))
            return "Sơ đồ State yêu cầu phải có tiêu đề (title).";
        if (error.Contains("State diagram must declare an initial state"))
            return "Sơ đồ State phải khai báo trạng thái bắt đầu (thuộc tính 'initialState').";
        if (error.Contains("State diagram must contain at least one state transition"))
            return "Sơ đồ State phải có ít nhất một bước chuyển trạng thái (thuộc tính 'transitions').";

        // Cú pháp
        if (error.Contains("Structural syntax error"))
            return "Lỗi cấu trúc cú pháp JSON: " + error.Substring(error.IndexOf(':') + 1).Trim();
        if (error.Contains("Diagram model is null"))
            return "Nội dung mô hình sơ đồ không được để trống.";

        return error;
    }
}
