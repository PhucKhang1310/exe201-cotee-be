using MongoDB.Bson;
using MongoDB.Driver;

namespace CooTee.Infrastructure.Repositories;





public class MongoRepository<T> : IMongoRepository<T> where T : class
{
    private readonly IMongoCollection<T> _collection;
    private readonly IMongoDatabase _database;

    public MongoRepository(IMongoDatabase database, string collectionName)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _collection = _database.GetCollection<T>(collectionName);
    }

    
    
    
    public async Task<T?> GetByIdAsync(string id)
    {
        try
        {
            var objectId = ObjectId.Parse(id);
            var filter = Builders<T>.Filter.Eq("_id", objectId);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
        catch (FormatException)
        {
            return null;
        }
    }

    
    
    
    public async Task<T?> FindOneAsync(Func<T, bool> predicate)
    {
        var results = await _collection.Find(_ => true).ToListAsync();
        return results.FirstOrDefault(predicate);
    }

    
    
    
    public async Task<T?> FindOneAsync(string filterField, object filterValue)
    {
        try
        {
            var filter = Builders<T>.Filter.Eq(filterField, filterValue);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
        catch
        {
            return null;
        }
    }

    
    
    
    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _collection.Find(_ => true).ToListAsync();
    }

    
    
    
    public async Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate)
    {
        var results = await _collection.Find(_ => true).ToListAsync();
        return results.Where(predicate);
    }

    
    
    
    public async Task<T> CreateAsync(T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        await _collection.InsertOneAsync(entity);
        return entity;
    }

    
    
    
    public async Task<IEnumerable<T>> CreateManyAsync(IEnumerable<T> entities)
    {
        var entityList = entities.ToList();
        if (!entityList.Any())
            throw new ArgumentException("Entities list cannot be empty", nameof(entities));

        await _collection.InsertManyAsync(entityList);
        return entityList;
    }

    
    
    
    public async Task<UpdateResult> UpdateAsync(string id, T entity)
    {
        try
        {
            var objectId = ObjectId.Parse(id);
            var filter = Builders<T>.Filter.Eq("_id", objectId);
            var updateOptions = new ReplaceOptions { IsUpsert = false };
            var result = await _collection.ReplaceOneAsync(filter, entity, updateOptions);

            return new UpdateResult
            {
                MatchedCount = result.MatchedCount,
                ModifiedCount = result.ModifiedCount
            };
        }
        catch (FormatException)
        {
            return new UpdateResult { MatchedCount = 0, ModifiedCount = 0 };
        }
    }

    
    
    
    public async Task<UpdateResult> UpdateAsync(Func<T, bool> predicate, T entity)
    {
        var results = await _collection.Find(_ => true).ToListAsync();
        var entityToUpdate = results.FirstOrDefault(predicate);

        if (entityToUpdate == null)
            return new UpdateResult { MatchedCount = 0, ModifiedCount = 0 };

        
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            throw new InvalidOperationException($"Entity type {typeof(T).Name} does not have an Id property");

        var idValue = idProperty.GetValue(entityToUpdate);
        return await UpdateAsync(idValue?.ToString() ?? string.Empty, entity);
    }

    
    
    
    public async Task<bool> DeleteAsync(string id)
    {
        try
        {
            var objectId = ObjectId.Parse(id);
            var filter = Builders<T>.Filter.Eq("_id", objectId);
            var result = await _collection.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    
    
    
    public async Task<long> DeleteAsync(Func<T, bool> predicate)
    {
        var results = await _collection.Find(_ => true).ToListAsync();
        var entitiesToDelete = results.Where(predicate).ToList();

        if (!entitiesToDelete.Any())
            return 0;

        long deletedCount = 0;
        foreach (var entity in entitiesToDelete)
        {
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty != null)
            {
                var idValue = idProperty.GetValue(entity);
                if (await DeleteAsync(idValue?.ToString() ?? string.Empty))
                    deletedCount++;
            }
        }

        return deletedCount;
    }

    
    
    
    public async Task<long> CountAsync(Func<T, bool> predicate)
    {
        var results = await _collection.Find(_ => true).ToListAsync();
        return results.Count(predicate);
    }

    
    
    
    public async Task<bool> ExistsAsync(string id)
    {
        var entity = await GetByIdAsync(id);
        return entity != null;
    }

    
    
    
    public async Task<bool> ExistsAsync(Func<T, bool> predicate)
    {
        var results = await _collection.Find(_ => true).ToListAsync();
        return results.Any(predicate);
    }
}
