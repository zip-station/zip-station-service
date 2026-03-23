using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IProjectApiKeyRepository : IBaseRepository<ProjectApiKey>
{
    Task<List<ProjectApiKey>> GetByProjectIdAsync(string projectId);
    Task<ProjectApiKey?> GetByKeyHashAsync(string keyHash);
}

public class ProjectApiKeyRepository : BaseRepository<ProjectApiKey>, IProjectApiKeyRepository
{
    public ProjectApiKeyRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<ProjectApiKey>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<ProjectApiKey>.Filter.Eq(k => k.ProjectId, projectId)
                   & Builders<ProjectApiKey>.Filter.Eq(k => k.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(k => k.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<ProjectApiKey?> GetByKeyHashAsync(string keyHash)
    {
        var filter = Builders<ProjectApiKey>.Filter.Eq(k => k.KeyHash, keyHash)
                   & Builders<ProjectApiKey>.Filter.Eq(k => k.IsRevoked, false)
                   & Builders<ProjectApiKey>.Filter.Eq(k => k.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }
}
