using Identity.Repositories.Entities;

namespace Identity.Repositories.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);
    Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        string? search,
        string? role,
        bool? isLocked,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
}
