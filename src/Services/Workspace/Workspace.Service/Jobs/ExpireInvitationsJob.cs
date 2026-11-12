using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UMLForge.BuildingBlocks.Jobs;
using Workspace.Repositories.Repositories;

namespace Workspace.Service.Jobs;

public class ExpireInvitationsJob : ScheduledJobBase
{
    public ExpireInvitationsJob(IServiceScopeFactory scopeFactory, ILogger<ExpireInvitationsJob> logger)
        : base(scopeFactory, logger) { }

    public override string JobName => "expire-invitations";
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    protected override async Task<string> RunOnceAsync(IServiceProvider scoped, CancellationToken ct)
    {
        var invitationRepo = scoped.GetRequiredService<IInvitationRepository>();
        var expiredInvitations = await invitationRepo.GetExpiredPendingInvitationsAsync(DateTime.UtcNow, ct);

        foreach (var inv in expiredInvitations)
        {
            inv.Status = "Expired";
            invitationRepo.Update(inv);
        }

        if (expiredInvitations.Count > 0)
        {
            await invitationRepo.SaveChangesAsync(ct);
        }

        return $"Expired {expiredInvitations.Count} invitations";
    }
}
