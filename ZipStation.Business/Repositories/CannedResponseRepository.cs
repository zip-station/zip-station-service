using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface ICannedResponseRepository : IBaseRepository<CannedResponse>
{
    Task<List<CannedResponse>> GetByProjectIdAsync(string projectId);
}

public class CannedResponseRepository : BaseRepository<CannedResponse>, ICannedResponseRepository
{
    public CannedResponseRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<CannedResponse>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<CannedResponse>.Filter.Eq(c => c.ProjectId, projectId)
                   & Builders<CannedResponse>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter)
            .SortBy(c => c.Title)
            .ToListAsync();
    }
}
