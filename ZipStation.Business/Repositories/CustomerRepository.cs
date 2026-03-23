using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface ICustomerRepository : IBaseRepository<Customer>
{
    Task<List<Customer>> GetByCompanyIdAsync(string companyId);
    Task<List<Customer>> GetByProjectIdAsync(string projectId);
    Task<Customer?> GetByEmailAndProjectAsync(string email, string projectId);
}

public class CustomerRepository : BaseRepository<Customer>, ICustomerRepository
{
    public CustomerRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<Customer>> GetByCompanyIdAsync(string companyId)
    {
        var filter = Builders<Customer>.Filter.Eq(c => c.CompanyId, companyId)
                   & Builders<Customer>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(c => c.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<List<Customer>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<Customer>.Filter.Eq(c => c.ProjectId, projectId)
                   & Builders<Customer>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(c => c.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<Customer?> GetByEmailAndProjectAsync(string email, string projectId)
    {
        var filter = Builders<Customer>.Filter.Eq(c => c.Email, email)
                   & Builders<Customer>.Filter.Eq(c => c.ProjectId, projectId)
                   & Builders<Customer>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }
}
