using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Workspace.Repositories.Data;

namespace Workspace.Repositories.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly WorkspaceDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(WorkspaceDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new[] { id }, ct);

    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.ToListAsync(ct);

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _dbSet.Where(predicate).ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await _dbSet.AddAsync(entity, ct);

    public virtual void Update(T entity)
        => _dbSet.Update(entity);

    public virtual void Delete(T entity)
        => _dbSet.Remove(entity);

    public virtual IQueryable<T> GetQueryable()
        => _dbSet.AsQueryable();

    public virtual async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
