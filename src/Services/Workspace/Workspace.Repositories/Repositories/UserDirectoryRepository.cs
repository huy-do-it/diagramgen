using Microsoft.EntityFrameworkCore;
using Workspace.Repositories.Data;
using Workspace.Repositories.Entities;

namespace Workspace.Repositories.Repositories;

public class UserDirectoryRepository : GenericRepository<UserDirectory>, IUserDirectoryRepository
{
    public UserDirectoryRepository(WorkspaceDbContext context) : base(context) { }

    public async Task<UserDirectory?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await _context.UserDirectory
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower(), ct);
    }

    public async Task<(IReadOnlyList<UserDirectory> Items, int TotalCount)> GetPagedUsersAsync(
        string? search, string? role, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.UserDirectory.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(searchLower) ||
                                     u.FullName.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleTrimmed = role.Trim();
            query = query.Where(u => u.Role == roleTrimmed);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task UpsertUserAsync(UserDirectory user, CancellationToken ct = default)
    {
        var existing = await _context.UserDirectory.FirstOrDefaultAsync(u => u.UserId == user.UserId, ct);
        if (existing != null)
        {
            existing.Username = user.Username;
            existing.FullName = user.FullName;
            existing.Role = user.Role;
            existing.IsLocked = user.IsLocked;
            _context.UserDirectory.Update(existing);
        }
        else
        {
            await _context.UserDirectory.AddAsync(user, ct);
        }
    }
}
