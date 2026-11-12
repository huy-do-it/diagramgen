using Workspace.Repositories.Entities;

namespace Workspace.Repositories.Repositories;

public interface IUserDirectoryRepository : IGenericRepository<UserDirectory>
{
    Task<UserDirectory?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<(IReadOnlyList<UserDirectory> Items, int TotalCount)> GetPagedUsersAsync(
        string? search, string? role, int page, int pageSize, CancellationToken ct = default);
    Task UpsertUserAsync(UserDirectory user, CancellationToken ct = default);
}
