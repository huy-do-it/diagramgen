using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Identity.Repositories.Data;

namespace Identity.Service.Jobs;

public class PurgeRefreshTokensJob : ScheduledJobBase
{
    public override string JobName => "purge-tokens";
    protected override TimeSpan Interval => TimeSpan.FromHours(6);
    protected override bool RunOnStartup => false;

    public PurgeRefreshTokensJob(
        IServiceScopeFactory scopeFactory,
        ILogger<PurgeRefreshTokensJob> logger)
        : base(scopeFactory, logger)
    {
    }

    protected override async Task<string> RunOnceAsync(IServiceProvider scoped, CancellationToken ct)
    {
        var db = scoped.GetRequiredService<IdentityDbContext>();
        var cutoff = DateTime.UtcNow.AddDays(-7);

        var tokensToDelete = await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff || (t.RevokedAt != null && t.RevokedAt < cutoff))
            .ToListAsync(ct);

        if (tokensToDelete.Count > 0)
        {
            db.RefreshTokens.RemoveRange(tokensToDelete);
            await db.SaveChangesAsync(ct);
        }

        return $"Purged {tokensToDelete.Count} expired or revoked refresh tokens older than {cutoff:yyyy-MM-dd HH:mm:ss} UTC.";
    }
}
