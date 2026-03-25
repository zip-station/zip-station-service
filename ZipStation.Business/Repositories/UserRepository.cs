using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IUserRepository : IBaseRepository<User>
{
    Task<User?> GetByFirebaseUserIdAsync(string firebaseUserId);
    Task<User?> GetByEmailAsync(string email);
    Task<List<User>> GetByCompanyIdAsync(string companyId);
    Task<User?> GetByInviteCodeAsync(string inviteCode);
}

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<User?> GetByFirebaseUserIdAsync(string firebaseUserId)
    {
        var filter = Builders<User>.Filter.Eq(u => u.FirebaseUserId, firebaseUserId)
                   & Builders<User>.Filter.Eq(u => u.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Email, email)
                   & Builders<User>.Filter.Eq(u => u.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<User>> GetByCompanyIdAsync(string companyId)
    {
        var filter = Builders<User>.Filter.ElemMatch(u => u.CompanyMemberships,
                        m => m.CompanyId == companyId)
                   & Builders<User>.Filter.Eq(u => u.IsVoid, false);
        return await _Collection.Find(filter).ToListAsync();
    }

    public async Task<User?> GetByInviteCodeAsync(string inviteCode)
    {
        var filter = Builders<User>.Filter.Eq(u => u.InviteCode, inviteCode)
                   & Builders<User>.Filter.Eq(u => u.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }
}
