using UMLForge.Diagram.Repositories.Enums;
using UMLForge.Diagram.Service.DTOs;

namespace UMLForge.Diagram.Service.Interfaces;

public interface IReviewService
{
    Task<ReviewDetailResponse> SubmitReviewAsync(CreateReviewRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<ReviewSummaryResponse>> GetReviewsAsync(Guid projectId, ReviewStatus? status = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<ReviewDetailResponse> GetReviewByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReviewDetailResponse> ApproveReviewAsync(Guid id, ReviewDecisionRequest? request = null, CancellationToken cancellationToken = default);
    Task<ReviewDetailResponse> RequestChangesAsync(Guid id, ReviewDecisionRequest? request = null, CancellationToken cancellationToken = default);
}
