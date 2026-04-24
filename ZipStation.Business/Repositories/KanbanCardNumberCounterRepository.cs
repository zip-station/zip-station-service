using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IKanbanCardNumberCounterRepository
{
    Task<long> GetNextCardNumberAsync(string projectId);
}

public class KanbanCardNumberCounterRepository : IKanbanCardNumberCounterRepository
{
    private readonly IMongoCollection<KanbanCardNumberCounter> _collection;

    public KanbanCardNumberCounterRepository(IMongoDatabase database, string collectionName)
    {
        _collection = database.GetCollection<KanbanCardNumberCounter>(collectionName);
    }

    public async Task<long> GetNextCardNumberAsync(string projectId)
    {
        var filter = Builders<KanbanCardNumberCounter>.Filter.Eq(c => c.ProjectId, projectId);
        var update = Builders<KanbanCardNumberCounter>.Update.Inc(c => c.CurrentValue, 1);
        var options = new FindOneAndUpdateOptions<KanbanCardNumberCounter>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var result = await _collection.FindOneAndUpdateAsync(filter, update, options);
        return result.CurrentValue;
    }
}
