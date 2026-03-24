using MongoDB.Driver;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;

namespace ZipStation.Business.Repositories;

public interface ITicketRepository : IBaseRepository<Ticket>
{
    Task<List<Ticket>> GetByProjectIdAsync(string projectId);
    Task<List<Ticket>> GetByCompanyIdAsync(string companyId);
    Task<long> CountByStatusAsync(string companyId, TicketStatus status);
    Task<Ticket?> GetByCustomerEmailAndProjectAsync(string email, string projectId);
    Task<Ticket?> GetByTicketNumberAsync(string companyId, long ticketNumber);
    Task<bool> ExistsByTicketNumberAndProjectAsync(string projectId, long ticketNumber);
}

public class TicketRepository : BaseRepository<Ticket>, ITicketRepository
{
    public TicketRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<Ticket>> GetByProjectIdAsync(string projectId)
    {
        var filter = Builders<Ticket>.Filter.Eq(t => t.ProjectId, projectId)
                   & Builders<Ticket>.Filter.Eq(t => t.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(t => t.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<List<Ticket>> GetByCompanyIdAsync(string companyId)
    {
        var filter = Builders<Ticket>.Filter.Eq(t => t.CompanyId, companyId)
                   & Builders<Ticket>.Filter.Eq(t => t.IsVoid, false);
        return await _Collection.Find(filter)
            .SortByDescending(t => t.CreatedOnDateTime)
            .ToListAsync();
    }

    public async Task<long> CountByStatusAsync(string companyId, TicketStatus status)
    {
        var filter = Builders<Ticket>.Filter.Eq(t => t.CompanyId, companyId)
                   & Builders<Ticket>.Filter.Eq(t => t.Status, status)
                   & Builders<Ticket>.Filter.Eq(t => t.IsVoid, false);
        return await _Collection.CountDocumentsAsync(filter);
    }

    public async Task<Ticket?> GetByTicketNumberAsync(string companyId, long ticketNumber)
    {
        var filter = Builders<Ticket>.Filter.Eq(t => t.CompanyId, companyId)
                   & Builders<Ticket>.Filter.Eq(t => t.TicketNumber, ticketNumber)
                   & Builders<Ticket>.Filter.Eq(t => t.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<Ticket?> GetByCustomerEmailAndProjectAsync(string email, string projectId)
    {
        var filter = Builders<Ticket>.Filter.Eq(t => t.CustomerEmail, email)
                   & Builders<Ticket>.Filter.Eq(t => t.ProjectId, projectId)
                   & Builders<Ticket>.Filter.Eq(t => t.IsVoid, false)
                   & Builders<Ticket>.Filter.In(t => t.Status, new[] { TicketStatus.Open, TicketStatus.Pending });
        return await _Collection.Find(filter)
            .SortByDescending(t => t.CreatedOnDateTime)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ExistsByTicketNumberAndProjectAsync(string projectId, long ticketNumber)
    {
        var filter = Builders<Ticket>.Filter.Eq(t => t.ProjectId, projectId)
                   & Builders<Ticket>.Filter.Eq(t => t.TicketNumber, ticketNumber);
        return await _Collection.CountDocumentsAsync(filter) > 0;
    }
}
