using Identity.Repositories.Entities;

namespace Identity.Repositories.Repositories;

public interface IRefreshTokenRepository : IGenericRepository<RefreshToken>
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task RevokeAllByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<int> PurgeExpiredAsync(DateTime cutoff, CancellationToken ct = default);
}
