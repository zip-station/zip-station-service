using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IProjectRepository : IBaseRepository<Project>
{
    Task<Project?> GetBySlugAsync(string companyId, string slug);
    Task<List<Project>> GetByCompanyIdAsync(string companyId);
}

public class ProjectRepository : BaseRepository<Project>, IProjectRepository
{
    public ProjectRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<Project?> GetBySlugAsync(string companyId, string slug)
    {
        var filter = Builders<Project>.Filter.Eq(p => p.CompanyId, companyId)
                   & Builders<Project>.Filter.Eq(p => p.Slug, slug)
                   & Builders<Project>.Filter.Eq(p => p.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<Project>> GetByCompanyIdAsync(string companyId)
    {
        var filter = Builders<Project>.Filter.Eq(p => p.CompanyId, companyId)
                   & Builders<Project>.Filter.Eq(p => p.IsVoid, false);
        return await _Collection.Find(filter).ToListAsync();
    }
}
