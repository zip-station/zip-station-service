using MongoDB.Driver;
using ZipStation.Models.Entities;

namespace ZipStation.Business.Repositories;

public interface IReportRepository : IBaseRepository<Report>
{
    Task<List<Report>> GetByCompanyIdAsync(string companyId);
}

public class ReportRepository : BaseRepository<Report>, IReportRepository
{
    public ReportRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<Report>> GetByCompanyIdAsync(string companyId)
    {
        var filter = Builders<Report>.Filter.Eq(r => r.CompanyId, companyId)
                   & Builders<Report>.Filter.Eq(r => r.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(r => r.CreatedOnDateTime)
            .ToListAsync();
    }
}
