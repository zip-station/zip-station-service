using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface ITicketMessageRepository : IBaseRepository<TicketMessage>
{
    Task<List<TicketMessage>> GetByTicketIdAsync(string ticketId);
}

public class TicketMessageRepository : BaseRepository<TicketMessage>, ITicketMessageRepository
{
    public TicketMessageRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<TicketMessage>> GetByTicketIdAsync(string ticketId)
    {
        var filter = Builders<TicketMessage>.Filter.Eq(m => m.TicketId, ticketId)
                   & Builders<TicketMessage>.Filter.Eq(m => m.IsVoid, false);
        return await _Collection.Find(filter)
            .SortBy(m => m.CreatedOnDateTime)
            .ToListAsync();
    }
}
