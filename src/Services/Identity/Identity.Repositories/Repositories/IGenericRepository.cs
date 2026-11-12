using System.Linq.Expressions;

namespace Identity.Repositories.Repositories;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
    IQueryable<T> GetQueryable();
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
