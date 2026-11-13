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
using UMLForge.Diagram.Service.Interfaces;

namespace UMLForge.Diagram.Service.Services;

public class ReviewService : IReviewService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IReviewRepository _reviewRepository;
    private readonly IDiagramRepository _diagramRepository;
    private readonly IMemberAccessRepository _memberAccessRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IOutbox? _outbox;

    public ReviewService(
        IReviewRepository reviewRepository,
        IDiagramRepository diagramRepository,
        IMemberAccessRepository memberAccessRepository,
        ICurrentUser currentUser,
        IOutbox? outbox = null)
    {
        _reviewRepository = reviewRepository;
        _diagramRepository = diagramRepository;
        _memberAccessRepository = memberAccessRepository;
        _currentUser = currentUser;
        _outbox = outbox;
    }

    public async Task<ReviewDetailResponse> SubmitReviewAsync(
        CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var projectId = request.ProjectId;
        var isAdmin = string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);

        // BR-29: Chỉ Leader mới được gửi review
        if (!isAdmin)
        {
            var access = await _memberAccessRepository.GetAccessAsync(projectId, _currentUser.UserId, cancellationToken);
            if (access == null)
            {
                throw new BusinessRuleException("BR-11", "You do not have access to this project.", 403);
            }

            if (!string.Equals(access.Role, ProjectRole.Leader, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException("BR-29", "Only project leader can submit project for review.", 403);
            }
        }

        // Lấy tất cả diagram chưa bị xóa của project
        var diagrams = await _diagramRepository.GetQueryable()
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        if (diagrams.Count == 0)
        {
            throw new BusinessRuleException("BR-29", "Project has no diagrams to submit for review.", 400);
        }

        // BR-29: Mọi diagram chưa xóa phải ở trạng thái Rendered
        var unrendered = diagrams
            .Where(d => d.Status != DiagramStatus.Rendered)
            .Select(d => new UnrenderedDiagramItem
            {
                Id = d.Id,
                Title = d.Title,
                Status = d.Status
            })
            .ToList();

        if (unrendered.Count > 0)
        {
            var errors = new Dictionary<string, string[]>
            {
                ["unrenderedDiagrams"] = unrendered.Select(u => $"{u.Title} (Id: {u.Id}, Status: {u.Status})").ToArray()
            };

            throw new BusinessRuleException(
                "BR-29",
                "All diagrams in project must be in Rendered status before submitting for review.",
                400)
            {
                Errors = errors
            };
        }

        // Tạo snapshot danh sách { diagramId, version, title }
        var snapshotItems = diagrams.Select(d => new DiagramSnapshotItem
        {
            DiagramId = d.Id,
            Version = d.CurrentVersion,
            Title = d.Title
        }).ToList();

        var snapshotJson = JsonSerializer.Serialize(snapshotItems, JsonOptions);
        var now = DateTime.UtcNow;

        var review = new Review
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = ReviewStatus.Submitted,
            SubmittedBy = _currentUser.UserId,
            SubmittedAt = now,
            SnapshotJson = snapshotJson
        };

        // Outbox event: ReviewSubmitted
        _outbox?.Add(StreamNames.Diagram, new ReviewSubmitted(
            review.Id,
            review.ProjectId,
            _currentUser.UserId
        ));

        await _reviewRepository.AddAsync(review, cancellationToken);
        await _reviewRepository.SaveChangesAsync(cancellationToken);

        return MapToDetail(review, snapshotItems);
    }

    public async Task<PagedResult<ReviewSummaryResponse>> GetReviewsAsync(
        Guid projectId,
        ReviewStatus? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        await EnsureProjectAccessAsync(projectId, cancellationToken);

        var totalItems = await _reviewRepository.CountReviewsAsync(projectId, status, cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var reviews = await _reviewRepository.GetReviewsPagedAsync(projectId, status, page, pageSize, cancellationToken);

        var items = reviews.Select(r => MapToSummary(r)).ToList();

        return new PagedResult<ReviewSummaryResponse>(items, page, pageSize, totalItems, totalPages);
    }

    public async Task<ReviewDetailResponse> GetReviewByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var review = await _reviewRepository.GetByIdAsync(id, cancellationToken);
        if (review == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Review with ID '{id}' was not found.", 404);
        }

        await EnsureProjectAccessAsync(review.ProjectId, cancellationToken);

        var snapshotItems = DeserializeSnapshot(review.SnapshotJson);
        return MapToDetail(review, snapshotItems);
    }

    public async Task<ReviewDetailResponse> ApproveReviewAsync(
        Guid id,
        ReviewDecisionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        return await DecideReviewAsync(id, ReviewStatus.Approved, request?.Note, cancellationToken);
    }

    public async Task<ReviewDetailResponse> RequestChangesAsync(
        Guid id,
        ReviewDecisionRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        return await DecideReviewAsync(id, ReviewStatus.ChangesRequested, request?.Note, cancellationToken);
    }

    private async Task<ReviewDetailResponse> DecideReviewAsync(
        Guid id,
        ReviewStatus targetStatus,
        string? note,
        CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetByIdAsync(id, cancellationToken);
        if (review == null)
        {
            throw new BusinessRuleException("NOT_FOUND", $"Review with ID '{id}' was not found.", 404);
        }

        var isAdmin = string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);

        // BR-30: Chỉ Lecturer của project mới được duyệt
        if (!isAdmin)
        {
            var access = await _memberAccessRepository.GetAccessAsync(review.ProjectId, _currentUser.UserId, cancellationToken);
            if (access == null)
            {
                throw new BusinessRuleException("BR-11", "You do not have access to this project.", 403);
            }

            if (!string.Equals(access.Role, ProjectRole.Lecturer, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException("BR-30", "Only the assigned lecturer can decide on reviews.", 403);
            }
        }

        if (review.Status != ReviewStatus.Submitted)
        {
            throw new BusinessRuleException("INVALID_STATE", $"Review is already in '{review.Status}' status and cannot be decided again.", 400);
        }

        var now = DateTime.UtcNow;
        review.Status = targetStatus;
        review.DecidedBy = _currentUser.UserId;
        review.DecidedAt = now;
        review.Note = note?.Trim();

        // Outbox event: ReviewDecided
        var decision = targetStatus == ReviewStatus.Approved ? ReviewDecisions.Approved : ReviewDecisions.ChangesRequested;
        _outbox?.Add(StreamNames.Diagram, new ReviewDecided(
            review.Id,
            review.ProjectId,
            decision,
            review.Note,
            _currentUser.UserId
        ));

        _reviewRepository.Update(review);
        await _reviewRepository.SaveChangesAsync(cancellationToken);

        var snapshotItems = DeserializeSnapshot(review.SnapshotJson);
        return MapToDetail(review, snapshotItems);
    }

    private async Task EnsureProjectAccessAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var isAdmin = string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (isAdmin) return;

        var hasAccess = await _memberAccessRepository.HasAccessAsync(projectId, _currentUser.UserId, cancellationToken);
        if (!hasAccess)
        {
            throw new BusinessRuleException("BR-11", "You do not have access to this project.", 403);
        }
    }

    private static List<DiagramSnapshotItem> DeserializeSnapshot(string snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson)) return new List<DiagramSnapshotItem>();
        try
        {
            return JsonSerializer.Deserialize<List<DiagramSnapshotItem>>(snapshotJson, JsonOptions) ?? new List<DiagramSnapshotItem>();
        }
        catch
        {
            return new List<DiagramSnapshotItem>();
        }
    }

    private static ReviewSummaryResponse MapToSummary(Review r)
    {
        var snapshot = DeserializeSnapshot(r.SnapshotJson);
        return new ReviewSummaryResponse
        {
            Id = r.Id,
            ProjectId = r.ProjectId,
            Status = r.Status,
            SubmittedBy = r.SubmittedBy,
            SubmittedAt = r.SubmittedAt,
            DecidedBy = r.DecidedBy,
            DecidedAt = r.DecidedAt,
            Note = r.Note,
            DiagramCount = snapshot.Count
        };
    }

    private static ReviewDetailResponse MapToDetail(Review r, List<DiagramSnapshotItem> snapshot)
    {
        return new ReviewDetailResponse
        {
            Id = r.Id,
            ProjectId = r.ProjectId,
            Status = r.Status,
            SubmittedBy = r.SubmittedBy,
            SubmittedAt = r.SubmittedAt,
            DecidedBy = r.DecidedBy,
            DecidedAt = r.DecidedAt,
            Note = r.Note,
            DiagramCount = snapshot.Count,
            SnapshotJson = r.SnapshotJson,
            Diagrams = snapshot
        };
    }
}
