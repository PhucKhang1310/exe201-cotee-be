namespace CooTee.Infrastructure.Repositories;





public interface IMongoRepository<T> where T : class
{
    
    
    
    
    
    Task<T?> GetByIdAsync(string id);

    
    
    
    
    
    Task<T?> FindOneAsync(Func<T, bool> predicate);

    
    
    
    
    
    Task<T?> FindOneAsync(string filterField, object filterValue);

    
    
    
    
    Task<IEnumerable<T>> GetAllAsync();

    
    
    
    
    
    Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate);

    
    
    
    
    
    Task<T> CreateAsync(T entity);

    
    
    
    
    
    Task<IEnumerable<T>> CreateManyAsync(IEnumerable<T> entities);

    
    
    
    
    
    
    Task<UpdateResult> UpdateAsync(string id, T entity);

    
    
    
    
    
    
    Task<UpdateResult> UpdateAsync(Func<T, bool> predicate, T entity);

    
    
    
    
    
    Task<bool> DeleteAsync(string id);

    
    
    
    
    
    Task<long> DeleteAsync(Func<T, bool> predicate);

    
    
    
    
    
    Task<long> CountAsync(Func<T, bool> predicate);

    
    
    
    
    
    Task<bool> ExistsAsync(string id);

    
    
    
    
    
    Task<bool> ExistsAsync(Func<T, bool> predicate);
}




public class UpdateResult
{
    public long MatchedCount { get; set; }
    public long ModifiedCount { get; set; }
    public bool IsSuccess => ModifiedCount > 0;
}
