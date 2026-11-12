using Microsoft.EntityFrameworkCore;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;

namespace Identity.Repositories.Repositories;

public class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(IdentityDbContext context) : base(context)
    {
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);
    }

    public async Task RevokeAllByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var activeTokens = await _dbSet
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }
    }

    public async Task<int> PurgeExpiredAsync(DateTime cutoff, CancellationToken ct = default)
    {
        var tokensToPurge = await _dbSet
            .Where(r => r.ExpiresAt < cutoff || (r.RevokedAt != null && r.RevokedAt < cutoff))
            .ToListAsync(ct);

        _dbSet.RemoveRange(tokensToPurge);
        return tokensToPurge.Count;
    }
}
