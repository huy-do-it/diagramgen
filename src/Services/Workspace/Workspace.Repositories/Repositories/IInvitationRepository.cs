using Workspace.Repositories.Entities;

namespace Workspace.Repositories.Repositories;

public interface IInvitationRepository : IGenericRepository<Invitation>
{
    Task<Invitation?> GetByIdWithProjectAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Invitation>> GetPendingForUserAsync(Guid inviteeId, CancellationToken ct = default);
    Task<int> GetPendingCountForProjectAsync(Guid projectId, CancellationToken ct = default);
    Task<bool> HasPendingInvitationAsync(Guid projectId, Guid inviteeId, CancellationToken ct = default);
    Task<IReadOnlyList<Invitation>> GetExpiredPendingInvitationsAsync(DateTime now, CancellationToken ct = default);
}
