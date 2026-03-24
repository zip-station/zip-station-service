using MongoDB.Driver;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;

namespace ZipStation.Business.Repositories;

public interface IIntakeEmailRepository : IBaseRepository<IntakeEmail>
{
    Task<List<IntakeEmail>> GetByCompanyIdAsync(string companyId);
    Task<List<IntakeEmail>> GetByProjectIdAsync(string projectId);
    Task<List<IntakeEmail>> GetPendingByProjectIdAsync(string projectId);
    Task<IntakeEmail?> GetByMessageIdAsync(string messageId);
    Task<long> DenyPendingByEmailAsync(string projectId, string fromEmail, string? deniedByUserId);
}

public class IntakeEmailRepository : BaseRepository<IntakeEmail>, IIntakeEmailRepository
{
    public IntakeEmailRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<IntakeEmail>> GetByCompanyIdAsync(string companyId)
    {
        var filter = Builders<IntakeEmail>.Filter.Eq(e => e.CompanyId, companyId)
                   & Builders<IntakeEmail>.Filter.Eq(e => e.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(e => e.ReceivedOn)
            .ToListAsync();
    }

    public async Task<List<IntakeEmail>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<IntakeEmail>.Filter.Eq(e => e.ProjectId, projectId)
                   & Builders<IntakeEmail>.Filter.Eq(e => e.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(e => e.ReceivedOn)
            .ToListAsync();
    }

    public async Task<List<IntakeEmail>> GetPendingByProjectIdAsync(string projectId)
    {
        var filter = Builders<IntakeEmail>.Filter.Eq(e => e.ProjectId, projectId)
                   & Builders<IntakeEmail>.Filter.Eq(e => e.Status, IntakeStatus.Pending)
                   & Builders<IntakeEmail>.Filter.Eq(e => e.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(e => e.ReceivedOn)
            .ToListAsync();
    }

    public async Task<IntakeEmail?> GetByMessageIdAsync(string messageId)
    {
        var filter = Builders<IntakeEmail>.Filter.Eq(e => e.MessageId, messageId)
                   & Builders<IntakeEmail>.Filter.Eq(e => e.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<long> DenyPendingByEmailAsync(string projectId, string fromEmail, string? deniedByUserId)
    {
        var filter = Builders<IntakeEmail>.Filter.Eq(e => e.ProjectId, projectId)
                   & Builders<IntakeEmail>.Filter.Eq(e => e.FromEmail, fromEmail)
                   & Builders<IntakeEmail>.Filter.Eq(e => e.Status, IntakeStatus.Pending)
                   & Builders<IntakeEmail>.Filter.Eq(e => e.IsVoid, false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var update = Builders<IntakeEmail>.Update
            .Set(e => e.Status, IntakeStatus.Denied)
            .Set(e => e.DeniedPermanently, true)
            .Set(e => e.DeniedByUserId, deniedByUserId)
            .Set(e => e.ProcessedOn, now)
            .Set(e => e.UpdatedOnDateTime, now);
        var result = await _Collection.UpdateManyAsync(filter, update);
        return result.ModifiedCount;
    }
}
