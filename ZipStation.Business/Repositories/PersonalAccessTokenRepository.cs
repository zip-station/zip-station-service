using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IPersonalAccessTokenRepository : IBaseRepository<PersonalAccessToken>
{
    Task<List<PersonalAccessToken>> GetByUserIdAsync(string userId, string companyId);
    Task<PersonalAccessToken?> GetByTokenHashAsync(string tokenHash);
    Task TouchLastUsedAsync(string id);
}

public class PersonalAccessTokenRepository : BaseRepository<PersonalAccessToken>, IPersonalAccessTokenRepository
{
    public PersonalAccessTokenRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<PersonalAccessToken>> GetByUserIdAsync(string userId, string companyId)
    {
        var filter = Builders<PersonalAccessToken>.Filter.Eq(t => t.UserId, userId)
                   & Builders<PersonalAccessToken>.Filter.Eq(t => t.CompanyId, companyId)
                   & Builders<PersonalAccessToken>.Filter.Eq(t => t.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(t => t.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<PersonalAccessToken?> GetByTokenHashAsync(string tokenHash)
    {
        var filter = Builders<PersonalAccessToken>.Filter.Eq(t => t.TokenHash, tokenHash)
                   & Builders<PersonalAccessToken>.Filter.Eq(t => t.IsRevoked, false)
                   & Builders<PersonalAccessToken>.Filter.Eq(t => t.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task TouchLastUsedAsync(string id)
    {
        var filter = Builders<PersonalAccessToken>.Filter.Eq(t => t.Id, id);
        var update = Builders<PersonalAccessToken>.Update
            .Set(t => t.LastUsedOnDateTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await _Collection.UpdateOneAsync(filter, update);
    }
}
