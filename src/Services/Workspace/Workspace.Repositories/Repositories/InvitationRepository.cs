using Microsoft.EntityFrameworkCore;
using Workspace.Repositories.Data;
using Workspace.Repositories.Entities;

namespace Workspace.Repositories.Repositories;

public class InvitationRepository : GenericRepository<Invitation>, IInvitationRepository
{
    public InvitationRepository(WorkspaceDbContext context) : base(context) { }

    public async Task<Invitation?> GetByIdWithProjectAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Invitations
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<IReadOnlyList<Invitation>> GetPendingForUserAsync(Guid inviteeId, CancellationToken ct = default)
    {
        return await _context.Invitations
            .Include(i => i.Project)
            .Where(i => i.InviteeId == inviteeId && i.Status == "Pending" && i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetPendingCountForProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        return await _context.Invitations
            .CountAsync(i => i.ProjectId == projectId && i.Status == "Pending" && i.ExpiresAt > DateTime.UtcNow, ct);
    }

    public async Task<bool> HasPendingInvitationAsync(Guid projectId, Guid inviteeId, CancellationToken ct = default)
    {
        return await _context.Invitations
            .AnyAsync(i => i.ProjectId == projectId && i.InviteeId == inviteeId && i.Status == "Pending" && i.ExpiresAt > DateTime.UtcNow, ct);
    }

    public async Task<IReadOnlyList<Invitation>> GetExpiredPendingInvitationsAsync(DateTime now, CancellationToken ct = default)
    {
        return await _context.Invitations
            .Where(i => i.Status == "Pending" && i.ExpiresAt <= now)
            .ToListAsync(ct);
    }
}
