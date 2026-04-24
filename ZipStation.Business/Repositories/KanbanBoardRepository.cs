using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IKanbanBoardRepository : IBaseRepository<KanbanBoard>
{
    Task<KanbanBoard?> GetByProjectIdAsync(string projectId);
}

public class KanbanBoardRepository : BaseRepository<KanbanBoard>, IKanbanBoardRepository
{
    public KanbanBoardRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<KanbanBoard?> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<KanbanBoard>.Filter.Eq(b => b.ProjectId, projectId)
                   & Builders<KanbanBoard>.Filter.Eq(b => b.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }
}
