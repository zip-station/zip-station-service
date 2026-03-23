using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface ITicketDraftRepository : IBaseRepository<TicketDraft>
{
    Task<TicketDraft?> GetByTicketAndUserAsync(string ticketId, string userId);
}

public class TicketDraftRepository : BaseRepository<TicketDraft>, ITicketDraftRepository
{
    public TicketDraftRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<TicketDraft?> GetByTicketAndUserAsync(string ticketId, string userId)
    {
        var filter = Builders<TicketDraft>.Filter.Eq(d => d.TicketId, ticketId)
                   & Builders<TicketDraft>.Filter.Eq(d => d.UserId, userId)
                   & Builders<TicketDraft>.Filter.Eq(d => d.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }
}
