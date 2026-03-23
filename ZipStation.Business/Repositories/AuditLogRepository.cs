using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IAuditLogRepository : IBaseRepository<AuditLogEntry>
{
    Task<List<AuditLogEntry>> GetByCompanyIdAsync(string companyId, int limit = 100);
    Task<List<AuditLogEntry>> GetByEntityAsync(string entityType, string entityId);
}

public class AuditLogRepository : BaseRepository<AuditLogEntry>, IAuditLogRepository
{
    public AuditLogRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<AuditLogEntry>> GetByCompanyIdAsync(string companyId, int limit = 100)
    {
        var filter = Builders<AuditLogEntry>.Filter.Eq(a => a.CompanyId, companyId)
                   & Builders<AuditLogEntry>.Filter.Eq(a => a.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(a => a.CreatedOnDateTime)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<List<AuditLogEntry>> GetByEntityAsync(string entityType, string entityId)
    {
        var filter = Builders<AuditLogEntry>.Filter.Eq(a => a.EntityType, entityType)
                   & Builders<AuditLogEntry>.Filter.Eq(a => a.EntityId, entityId)
                   & Builders<AuditLogEntry>.Filter.Eq(a => a.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(a => a.CreatedOnDateTime)
            .ToListAsync();
    }
}
