using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IMaxQuestionRepository : IBaseRepository<MaxQuestion>
{
    Task<List<MaxQuestion>> GetByProjectIdAsync(string projectId);
    Task<List<MaxQuestion>> GetByTicketIdAsync(string ticketId);
    Task<List<MaxQuestion>> GetByStoryIdAsync(string storyId);
    Task<List<MaxQuestion>> GetPendingByProjectIdAsync(string projectId);
    Task<List<MaxQuestion>> GetByStatusAndProjectIdAsync(string projectId, string status);
    Task<long> SoftDeletePendingByTicketIdAsync(string ticketId);
    Task<long> SoftDeletePendingByStoryIdAsync(string storyId);
}

public class MaxQuestionRepository : BaseRepository<MaxQuestion>, IMaxQuestionRepository
{
    public MaxQuestionRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<MaxQuestion>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<MaxQuestion>.Filter.Eq(q => q.ProjectId, projectId)
                   & Builders<MaxQuestion>.Filter.Eq(q => q.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(q => q.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<List<MaxQuestion>> GetByTicketIdAsync(string ticketId)
    {
        var filter = Builders<MaxQuestion>.Filter.Eq(q => q.SourceTicketId, ticketId)
                   & Builders<MaxQuestion>.Filter.Eq(q => q.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(q => q.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<List<MaxQuestion>> GetPendingByProjectIdAsync(string projectId)
        => await GetByStatusAndProjectIdAsync(projectId, "pending");

    public async Task<List<MaxQuestion>> GetByStatusAndProjectIdAsync(string projectId, string status)
    {
        var filter = Builders<MaxQuestion>.Filter.Eq(q => q.ProjectId, projectId)
                   & Builders<MaxQuestion>.Filter.Eq(q => q.Status, status)
                   & Builders<MaxQuestion>.Filter.Eq(q => q.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(q => q.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<long> SoftDeletePendingByTicketIdAsync(string ticketId)
    {
        var filter = Builders<MaxQuestion>.Filter.Eq(q => q.SourceTicketId, ticketId)
                   & Builders<MaxQuestion>.Filter.Eq(q => q.Status, "pending")
                   & Builders<MaxQuestion>.Filter.Eq(q => q.IsVoid, false);
        var update = Builders<MaxQuestion>.Update
            .Set(q => q.IsVoid, true)
            .Set(q => q.UpdatedOnDateTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = await _Collection.UpdateManyAsync(filter, update);
        return result.ModifiedCount;
    }

    public async Task<List<MaxQuestion>> GetByStoryIdAsync(string storyId)
    {
        var filter = Builders<MaxQuestion>.Filter.Eq(q => q.SourceStoryId, storyId)
                   & Builders<MaxQuestion>.Filter.Eq(q => q.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(q => q.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<long> SoftDeletePendingByStoryIdAsync(string storyId)
    {
        var filter = Builders<MaxQuestion>.Filter.Eq(q => q.SourceStoryId, storyId)
                   & Builders<MaxQuestion>.Filter.Eq(q => q.Status, "pending")
                   & Builders<MaxQuestion>.Filter.Eq(q => q.IsVoid, false);
        var update = Builders<MaxQuestion>.Update
            .Set(q => q.IsVoid, true)
            .Set(q => q.UpdatedOnDateTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = await _Collection.UpdateManyAsync(filter, update);
        return result.ModifiedCount;
    }
}
