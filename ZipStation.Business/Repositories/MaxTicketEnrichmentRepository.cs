using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IMaxTicketEnrichmentRepository : IBaseRepository<MaxTicketEnrichment>
{
    Task<MaxTicketEnrichment?> GetByTicketIdAsync(string ticketId);
    Task<List<MaxTicketEnrichment>> GetRecentByProjectIdAsync(string projectId, int limit);
}

public class MaxTicketEnrichmentRepository : BaseRepository<MaxTicketEnrichment>, IMaxTicketEnrichmentRepository
{
    public MaxTicketEnrichmentRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<MaxTicketEnrichment?> GetByTicketIdAsync(string ticketId)
    {
        var filter = Builders<MaxTicketEnrichment>.Filter.Eq(e => e.TicketId, ticketId)
                   & Builders<MaxTicketEnrichment>.Filter.Eq(e => e.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<MaxTicketEnrichment>> GetRecentByProjectIdAsync(string projectId, int limit)
    {
        var filter = Builders<MaxTicketEnrichment>.Filter.Eq(e => e.ProjectId, projectId)
                   & Builders<MaxTicketEnrichment>.Filter.Eq(e => e.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(e => e.CreatedOnDateTime)
            .Limit(limit)
            .ToListAsync();
    }
}
