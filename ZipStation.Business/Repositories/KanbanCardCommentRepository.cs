using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IKanbanCardCommentRepository : IBaseRepository<KanbanCardComment>
{
    Task<List<KanbanCardComment>> GetByCardIdAsync(string cardId);
}

public class KanbanCardCommentRepository : BaseRepository<KanbanCardComment>, IKanbanCardCommentRepository
{
    public KanbanCardCommentRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<KanbanCardComment>> GetByCardIdAsync(string cardId)
    {
        var filter = Builders<KanbanCardComment>.Filter.Eq(c => c.CardId, cardId)
                   & Builders<KanbanCardComment>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter)
            .SortBy(c => c.CreatedOnDateTime)
            .ToListAsync();
    }
}
