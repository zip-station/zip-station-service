using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IAlertRepository : IBaseRepository<Alert>
{
    Task<List<Alert>> GetByProjectIdAsync(string projectId);
    Task<List<Alert>> GetEnabledByProjectIdAsync(string projectId);
}

public class AlertRepository : BaseRepository<Alert>, IAlertRepository
{
    public AlertRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<Alert>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<Alert>.Filter.Eq(a => a.ProjectId, projectId)
                   & Builders<Alert>.Filter.Eq(a => a.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(a => a.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<List<Alert>> GetEnabledByProjectIdAsync(string projectId)
    {
        var filter = Builders<Alert>.Filter.Eq(a => a.ProjectId, projectId)
                   & Builders<Alert>.Filter.Eq(a => a.IsEnabled, true)
                   & Builders<Alert>.Filter.Eq(a => a.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(a => a.CreatedOnDateTime)
            .ToListAsync();
    }
}
