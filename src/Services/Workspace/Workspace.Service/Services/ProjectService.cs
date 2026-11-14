using FluentValidation;
using Microsoft.AspNetCore.Http;
using UMLForge.BuildingBlocks.Auth;
using UMLForge.BuildingBlocks.Outbox;
using UMLForge.BuildingBlocks.Web;
using UMLForge.Contracts.Events;
using Workspace.Repositories.Entities;
using Workspace.Repositories.Repositories;
using Workspace.Service.DTOs;
using Workspace.Service.Interfaces;

namespace Workspace.Service.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepo;
    private readonly IUserDirectoryRepository _userDirectoryRepo;
    private readonly IOutbox _outbox;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateProjectRequest> _createValidator;
    private readonly IValidator<UpdateProjectRequest> _updateValidator;

    public ProjectService(
        IProjectRepository projectRepo,
        IUserDirectoryRepository userDirectoryRepo,
        IOutbox outbox,
        ICurrentUser currentUser,
        IValidator<CreateProjectRequest> createValidator,
        IValidator<UpdateProjectRequest> updateValidator)
    {
        _projectRepo = projectRepo;
        _userDirectoryRepo = userDirectoryRepo;
        _outbox = outbox;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<ProjectDetailResponse> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);

        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            OwnerId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray()
        };

        // BR-06: Creator automatically becomes Leader
        var leaderMember = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = _currentUser.UserId,
            Role = "Leader",
            JoinedAt = DateTime.UtcNow
        };
        project.Members.Add(leaderMember);

        await _projectRepo.AddAsync(project, ct);

        // Stage ProjectCreated event in Outbox
        _outbox.Add(StreamNames.Workspace, new ProjectCreated(project.Id, project.Name, project.OwnerId, _currentUser.UserId));

        await _projectRepo.SaveChangesAsync(ct);

        return await ToDetailResponseAsync(project, ct);
    }

    public async Task<PagedResult<ProjectSummaryResponse>> GetProjectsAsync(PageQuery query, string? role, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var (items, totalCount) = await _projectRepo.GetPagedProjectsForUserAsync(
            _currentUser.UserId, query.Search, role, query.Sort, query.Page, query.PageSize, ct);

        var dtos = items.Select(p => new ProjectSummaryResponse(
            p.Id,
            p.Name,
            p.Description,
            p.OwnerId,
            p.LecturerId,
            p.Members.Count,
            p.CreatedAt
        )).ToList();

        return PagedResult<ProjectSummaryResponse>.Create(dtos, query.Page, query.PageSize, totalCount);
    }

    public async Task<ProjectDetailResponse> GetProjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var project = await _projectRepo.GetByIdWithMembersAsync(id, ct);
        if (project == null)
            throw new NotFoundException("Không tìm thấy dự án.");

        var isMember = project.Members.Any(m => m.UserId == _currentUser.UserId) || project.LecturerId == _currentUser.UserId;
        if (!isMember)
            throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền truy cập dự án này.");

        return await ToDetailResponseAsync(project, ct);
    }

    public async Task<ProjectDetailResponse> UpdateProjectAsync(Guid id, UpdateProjectRequest request, string? ifMatch, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);

        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var project = await _projectRepo.GetByIdWithMembersAsync(id, ct);
        if (project == null)
            throw new NotFoundException("Không tìm thấy dự án.");

        // BR-08: Only Leader can update project
        var isLeader = project.OwnerId == _currentUser.UserId ||
                       project.Members.Any(m => m.UserId == _currentUser.UserId && m.Role == "Leader");
        if (!isLeader)
            throw new ForbiddenException("BR-08", "Chỉ Team Leader mới có quyền cập nhật dự án.");

        // Optimistic concurrency check (BR-16)
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new BusinessRuleException("CONCURRENCY_CONFLICT", "Header If-Match là bắt buộc.", StatusCodes.Status409Conflict);
        }

        var cleanIfMatch = ifMatch.Trim().Trim('"');
        if (project.RowVersion != null && project.RowVersion.Length > 0)
        {
            var currentETag = Convert.ToBase64String(project.RowVersion);
            if (!string.Equals(cleanIfMatch, currentETag, StringComparison.Ordinal))
            {
                throw new BusinessRuleException("CONCURRENCY_CONFLICT", "Dữ liệu đã bị thay đổi bởi người khác.", StatusCodes.Status409Conflict);
            }
        }

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        project.RowVersion = Guid.NewGuid().ToByteArray();

        _projectRepo.Update(project);
        await _projectRepo.SaveChangesAsync(ct);

        return await ToDetailResponseAsync(project, ct);
    }

    public async Task DeleteProjectAsync(Guid id, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var project = await _projectRepo.GetByIdWithMembersAsync(id, ct);
        if (project == null)
            throw new NotFoundException("Không tìm thấy dự án.");

        // BR-08: Only Leader can delete project
        var isLeader = project.OwnerId == _currentUser.UserId ||
                       project.Members.Any(m => m.UserId == _currentUser.UserId && m.Role == "Leader");
        if (!isLeader)
            throw new ForbiddenException("BR-08", "Chỉ Team Leader mới có quyền xóa dự án.");

        // Soft delete
        project.IsDeleted = true;
        _projectRepo.Update(project);

        // Stage ProjectDeleted event in Outbox
        _outbox.Add(StreamNames.Workspace, new ProjectDeleted(project.Id, _currentUser.UserId));

        await _projectRepo.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectMemberResponse>> GetMembersAsync(Guid projectId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var isMember = await _projectRepo.IsMemberAsync(projectId, _currentUser.UserId, ct);
        if (!isMember)
            throw new ForbiddenException("FORBIDDEN", "Bạn không có quyền truy cập dự án này.");

        var members = await _projectRepo.GetMembersAsync(projectId, ct);
        var directory = await _userDirectoryRepo.GetByIdsAsync(members.Select(m => m.UserId), ct);
        return members.Select(m => ProjectMemberResponse.From(m, directory)).ToList();
    }

    public async Task RemoveMemberAsync(Guid projectId, Guid memberUserId, CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("Người dùng chưa đăng nhập.");

        var project = await _projectRepo.GetByIdWithMembersAsync(projectId, ct);
        if (project == null)
            throw new NotFoundException("Không tìm thấy dự án.");

        // BR-08: Only Leader can remove members
        var isLeader = project.OwnerId == _currentUser.UserId ||
                       project.Members.Any(m => m.UserId == _currentUser.UserId && m.Role == "Leader");
        if (!isLeader)
            throw new ForbiddenException("BR-08", "Chỉ Team Leader mới có quyền xóa thành viên.");

        var member = project.Members.FirstOrDefault(m => m.UserId == memberUserId);
        if (member == null)
            throw new NotFoundException("Thành viên không tồn tại trong dự án.");

        // BR-08: Leader cannot be removed
        if (member.Role == "Leader" || memberUserId == project.OwnerId)
            throw new ForbiddenException("BR-08", "Không thể xóa Leader khỏi dự án.");

        _projectRepo.RemoveMember(member);

        if (project.LecturerId == memberUserId)
        {
            project.LecturerId = null;
            _projectRepo.Update(project);
        }

        // Stage MemberRemoved event in Outbox
        _outbox.Add(StreamNames.Workspace, new MemberRemoved(projectId, memberUserId, _currentUser.UserId));

        await _projectRepo.SaveChangesAsync(ct);
    }

    private async Task<ProjectDetailResponse> ToDetailResponseAsync(Project p, CancellationToken ct)
    {
        var directory = await _userDirectoryRepo.GetByIdsAsync(p.Members.Select(m => m.UserId), ct);
        var memberDtos = p.Members
            .Select(m => ProjectMemberResponse.From(m, directory))
            .ToList();

        return new ProjectDetailResponse(
            p.Id,
            p.Name,
            p.Description,
            p.OwnerId,
            p.LecturerId,
            p.CreatedAt,
            p.RowVersion,
            memberDtos
        );
    }
}
