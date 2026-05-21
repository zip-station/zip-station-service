using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IMaxStoryEnrichmentRepository : IBaseRepository<MaxStoryEnrichment>
{
    Task<MaxStoryEnrichment?> GetByStoryIdAsync(string storyId);
    Task<List<MaxStoryEnrichment>> GetRecentByProjectIdAsync(string projectId, int limit);
    // Story-id-keyed upsert (not BaseEntity-id-keyed). Hides the base's id-keyed UpsertAsync intentionally.
    new Task<MaxStoryEnrichment> UpsertAsync(MaxStoryEnrichment entity);
}

public class MaxStoryEnrichmentRepository : BaseRepository<MaxStoryEnrichment>, IMaxStoryEnrichmentRepository
{
    public MaxStoryEnrichmentRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<MaxStoryEnrichment?> GetByStoryIdAsync(string storyId)
    {
        var filter = Builders<MaxStoryEnrichment>.Filter.Eq(e => e.StoryId, storyId)
                   & Builders<MaxStoryEnrichment>.Filter.Eq(e => e.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<MaxStoryEnrichment>> GetRecentByProjectIdAsync(string projectId, int limit)
    {
        var filter = Builders<MaxStoryEnrichment>.Filter.Eq(e => e.ProjectId, projectId)
                   & Builders<MaxStoryEnrichment>.Filter.Eq(e => e.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(e => e.CreatedOnDateTime)
            .Limit(limit)
            .ToListAsync();
    }

    public new async Task<MaxStoryEnrichment> UpsertAsync(MaxStoryEnrichment entity)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        entity.UpdatedOnDateTime = now;

        // Carry forward the existing _id (and CreatedOnDateTime) when a doc for this StoryId
        // already exists — Mongo treats _id as immutable on replace, and BaseEntity's default
        // ctor auto-generates a fresh ObjectId, so callers that pass a brand-new entity would
        // otherwise hit "the (immutable) field '_id' was found to have been altered".
        var filter = Builders<MaxStoryEnrichment>.Filter.Eq(e => e.StoryId, entity.StoryId);
        var existing = await _Collection.Find(filter).FirstOrDefaultAsync();
        if (existing != null)
        {
            entity.Id = existing.Id;
            entity.CreatedOnDateTime = existing.CreatedOnDateTime;
        }
        else
        {
            if (string.IsNullOrEmpty(entity.Id))
                entity.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
            if (entity.CreatedOnDateTime == 0)
                entity.CreatedOnDateTime = now;
        }

        var options = new ReplaceOptions { IsUpsert = true };
        await _Collection.ReplaceOneAsync(filter, entity, options);
        return entity;
    }
}
