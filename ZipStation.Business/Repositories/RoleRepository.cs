using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IRoleRepository : IBaseRepository<Role>
{
    Task<List<Role>> GetByCompanyIdAsync(string companyId);
    Task<Role?> GetByNameAndCompanyAsync(string name, string companyId);
    Task<List<Role>> GetByIdsAsync(IEnumerable<string> ids);
}

public class RoleRepository : BaseRepository<Role>, IRoleRepository
{
    public RoleRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<Role>> GetByCompanyIdAsync(string companyId)
    {
        var filter = Builders<Role>.Filter.Eq(r => r.CompanyId, companyId)
                   & Builders<Role>.Filter.Eq(r => r.IsVoid, false);
        return await _Collection.Find(filter).SortBy(r => r.Name).ToListAsync();
    }

    public async Task<Role?> GetByNameAndCompanyAsync(string name, string companyId)
    {
        var filter = Builders<Role>.Filter.Eq(r => r.CompanyId, companyId)
                   & Builders<Role>.Filter.Eq(r => r.Name, name)
                   & Builders<Role>.Filter.Eq(r => r.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<Role>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var filter = Builders<Role>.Filter.In(r => r.Id, ids)
                   & Builders<Role>.Filter.Eq(r => r.IsVoid, false);
        return await _Collection.Find(filter).ToListAsync();
    }
}
