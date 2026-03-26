using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface ICompanyRepository : IBaseRepository<Company>
{
    Task<Company?> GetBySlugAsync(string slug);
    Task<Company?> GetFirstAsync();
    Task<List<Company>> GetByOwnerUserIdAsync(string userId);
    Task<List<Company>> GetByIdsAsync(List<string> ids);
}

public class CompanyRepository : BaseRepository<Company>, ICompanyRepository
{
    public CompanyRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<Company?> GetFirstAsync()
    {
        var filter = Builders<Company>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<Company?> GetBySlugAsync(string slug)
    {
        var filter = Builders<Company>.Filter.Eq(c => c.Slug, slug)
                   & Builders<Company>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<Company>> GetByOwnerUserIdAsync(string userId)
    {
        var filter = Builders<Company>.Filter.Eq(c => c.OwnerUserId, userId)
                   & Builders<Company>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter).ToListAsync();
    }

    public async Task<List<Company>> GetByIdsAsync(List<string> ids)
    {
        var filter = Builders<Company>.Filter.In(c => c.Id, ids)
                   & Builders<Company>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter).ToListAsync();
    }
}
