using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IMaxTaskRepository : IBaseRepository<MaxTask>
{
    Task<List<MaxTask>> GetByTicketIdAsync(string ticketId);
    Task<List<MaxTask>> GetPendingByProjectIdAsync(string projectId);
    Task<long> SoftDeletePendingByTicketIdAsync(string ticketId);
}

public class MaxTaskRepository : BaseRepository<MaxTask>, IMaxTaskRepository
{
    public MaxTaskRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<MaxTask>> GetByTicketIdAsync(string ticketId)
    {
        var filter = Builders<MaxTask>.Filter.Eq(t => t.TicketId, ticketId)
                   & Builders<MaxTask>.Filter.Eq(t => t.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(t => t.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<List<MaxTask>> GetPendingByProjectIdAsync(string projectId)
    {
        var filter = Builders<MaxTask>.Filter.Eq(t => t.ProjectId, projectId)
                   & Builders<MaxTask>.Filter.Eq(t => t.Status, "pending")
                   & Builders<MaxTask>.Filter.Eq(t => t.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(t => t.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<long> SoftDeletePendingByTicketIdAsync(string ticketId)
    {
        var filter = Builders<MaxTask>.Filter.Eq(t => t.TicketId, ticketId)
                   & Builders<MaxTask>.Filter.Eq(t => t.Status, "pending")
                   & Builders<MaxTask>.Filter.Eq(t => t.IsVoid, false);
        var update = Builders<MaxTask>.Update
            .Set(t => t.IsVoid, true)
            .Set(t => t.UpdatedOnDateTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = await _Collection.UpdateManyAsync(filter, update);
        return result.ModifiedCount;
    }
}
