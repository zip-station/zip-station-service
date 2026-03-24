using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface ITicketIdCounterRepository
{
    Task<long> GetNextTicketNumberAsync(string projectId);
    Task<long> GetCurrentValueAsync(string projectId);
    Task SetValueAsync(string projectId, long value);
}

public class TicketIdCounterRepository : ITicketIdCounterRepository
{
    private readonly IMongoCollection<TicketIdCounter> _collection;

    public TicketIdCounterRepository(IMongoDatabase database, string collectionName)
    {
        _collection = database.GetCollection<TicketIdCounter>(collectionName);
    }

    public async Task<long> GetNextTicketNumberAsync(string projectId)
    {
        var filter = Builders<TicketIdCounter>.Filter.Eq(c => c.ProjectId, projectId);
        var update = Builders<TicketIdCounter>.Update.Inc(c => c.CurrentValue, 1);
        var options = new FindOneAndUpdateOptions<TicketIdCounter>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var result = await _collection.FindOneAndUpdateAsync(filter, update, options);
        return result.CurrentValue;
    }

    public async Task<long> GetCurrentValueAsync(string projectId)
    {
        var filter = Builders<TicketIdCounter>.Filter.Eq(c => c.ProjectId, projectId);
        var counter = await _collection.Find(filter).FirstOrDefaultAsync();
        return counter?.CurrentValue ?? 0;
    }

    public async Task SetValueAsync(string projectId, long value)
    {
        var filter = Builders<TicketIdCounter>.Filter.Eq(c => c.ProjectId, projectId);
        var update = Builders<TicketIdCounter>.Update.Set(c => c.CurrentValue, value);
        var options = new UpdateOptions { IsUpsert = true };
        await _collection.UpdateOneAsync(filter, update, options);
    }
}
