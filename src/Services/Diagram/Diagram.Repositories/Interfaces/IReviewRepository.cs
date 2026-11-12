using UMLForge.Diagram.Repositories.Entities;
using UMLForge.Diagram.Repositories.Enums;

namespace UMLForge.Diagram.Repositories.Interfaces;

public interface IReviewRepository : IGenericRepository<Review>
{
    Task<Review?> GetLatestReviewByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Review>> GetReviewsPagedAsync(Guid projectId, ReviewStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountReviewsAsync(Guid projectId, ReviewStatus? status, CancellationToken cancellationToken = default);
}
