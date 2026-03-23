using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IIntakeRuleRepository : IBaseRepository<IntakeRule>
{
    Task<List<IntakeRule>> GetByProjectIdAsync(string projectId);
    Task<List<IntakeRule>> GetEnabledByProjectIdAsync(string projectId);
}

public class IntakeRuleRepository : BaseRepository<IntakeRule>, IIntakeRuleRepository
{
    public IntakeRuleRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<IntakeRule>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<IntakeRule>.Filter.Eq(r => r.ProjectId, projectId)
                   & Builders<IntakeRule>.Filter.Eq(r => r.IsVoid, false);
        return await _Collection.Find(filter)
            .SortBy(r => r.Priority)
            .ToListAsync();
    }

    public async Task<List<IntakeRule>> GetEnabledByProjectIdAsync(string projectId)
    {
        var filter = Builders<IntakeRule>.Filter.Eq(r => r.ProjectId, projectId)
                   & Builders<IntakeRule>.Filter.Eq(r => r.IsEnabled, true)
                   & Builders<IntakeRule>.Filter.Eq(r => r.IsVoid, false);
        return await _Collection.Find(filter)
            .SortBy(r => r.Priority)
            .ToListAsync();
    }
}
