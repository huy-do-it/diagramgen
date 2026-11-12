using Microsoft.EntityFrameworkCore;
using Identity.Repositories.Data;
using Identity.Repositories.Entities;

namespace Identity.Repositories.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(IdentityDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToLower();
        return await _dbSet
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized, ct);
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToLower();
        return await _dbSet.AnyAsync(u => u.Username.ToLower() == normalized, ct);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        string? search,
        string? role,
        bool? isLocked,
        string? sort,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(searchLower) || u.FullName.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        if (isLocked.HasValue)
        {
            query = query.Where(u => u.IsLocked == isLocked.Value);
        }

        var totalCount = await query.CountAsync(ct);

        // Sorting
        query = (sort?.ToLower()) switch
        {
            "username" or "username:asc" => query.OrderBy(u => u.Username),
            "username:desc" => query.OrderByDescending(u => u.Username),
            "createdat:asc" => query.OrderBy(u => u.CreatedAt),
            _ => query.OrderByDescending(u => u.CreatedAt) // default createdAt:desc
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
