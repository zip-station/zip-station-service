using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IMaxExampleReplyRepository : IBaseRepository<MaxExampleReply>
{
    Task<List<MaxExampleReply>> GetByProjectIdAsync(string projectId);
}

public class MaxExampleReplyRepository : BaseRepository<MaxExampleReply>, IMaxExampleReplyRepository
{
    public MaxExampleReplyRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<MaxExampleReply>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<MaxExampleReply>.Filter.Eq(r => r.ProjectId, projectId)
                   & Builders<MaxExampleReply>.Filter.Eq(r => r.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(r => r.CreatedOnDateTime)
            .ToListAsync();
    }
}
