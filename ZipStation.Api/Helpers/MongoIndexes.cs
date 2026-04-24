using MongoDB.Driver;
using ZipStation.Business.Helpers;
using ZipStation.Models.Entities;

namespace ZipStation.Api.Helpers;

public static class MongoIndexes
{
    public static async Task EnsureIndexesAsync(IMongoDatabase database, AppConfig appConfig)
    {
        var collections = appConfig.ZipStationMongoDb.Collections;

        // Companies
        var companies = database.GetCollection<Company>(collections.Companies);
        await companies.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Company>(Builders<Company>.IndexKeys.Ascending(c => c.Slug)),
            new CreateIndexModel<Company>(Builders<Company>.IndexKeys.Ascending(c => c.OwnerUserId)),
        });

        // Projects
        var projects = database.GetCollection<Project>(collections.Projects);
        await projects.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Project>(Builders<Project>.IndexKeys.Ascending(p => p.CompanyId)),
            new CreateIndexModel<Project>(Builders<Project>.IndexKeys.Combine(
                Builders<Project>.IndexKeys.Ascending(p => p.CompanyId),
                Builders<Project>.IndexKeys.Ascending(p => p.Slug))),
        });

        // Users
        var users = database.GetCollection<User>(collections.Users);
        await users.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.FirebaseUserId)),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.Email)),
        });

        // Tickets
        var tickets = database.GetCollection<Ticket>(collections.Tickets);
        await tickets.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(t => t.CompanyId)),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(t => t.ProjectId)),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Combine(
                Builders<Ticket>.IndexKeys.Ascending(t => t.CompanyId),
                Builders<Ticket>.IndexKeys.Ascending(t => t.Status))),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(t => t.TicketNumber)),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Ascending(t => t.CustomerEmail)),
            new CreateIndexModel<Ticket>(Builders<Ticket>.IndexKeys.Text(t => t.Subject)),
        });

        // Ticket Messages
        var ticketMessages = database.GetCollection<TicketMessage>(collections.TicketMessages);
        await ticketMessages.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TicketMessage>(Builders<TicketMessage>.IndexKeys.Ascending(m => m.TicketId)),
            new CreateIndexModel<TicketMessage>(Builders<TicketMessage>.IndexKeys.Ascending(m => m.CompanyId)),
        });

        // Customers
        var customers = database.GetCollection<Customer>(collections.Customers);
        await customers.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Customer>(Builders<Customer>.IndexKeys.Ascending(c => c.CompanyId)),
            new CreateIndexModel<Customer>(Builders<Customer>.IndexKeys.Ascending(c => c.ProjectId)),
            new CreateIndexModel<Customer>(Builders<Customer>.IndexKeys.Combine(
                Builders<Customer>.IndexKeys.Ascending(c => c.Email),
                Builders<Customer>.IndexKeys.Ascending(c => c.ProjectId))),
        });

        // Intake Emails
        var intakeEmails = database.GetCollection<IntakeEmail>(collections.IntakeEmails);
        await intakeEmails.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<IntakeEmail>(Builders<IntakeEmail>.IndexKeys.Ascending(e => e.CompanyId)),
            new CreateIndexModel<IntakeEmail>(Builders<IntakeEmail>.IndexKeys.Ascending(e => e.ProjectId)),
            new CreateIndexModel<IntakeEmail>(Builders<IntakeEmail>.IndexKeys.Combine(
                Builders<IntakeEmail>.IndexKeys.Ascending(e => e.ProjectId),
                Builders<IntakeEmail>.IndexKeys.Ascending(e => e.Status))),
            new CreateIndexModel<IntakeEmail>(Builders<IntakeEmail>.IndexKeys.Ascending(e => e.MessageId)),
        });

        // Intake Rules
        var intakeRules = database.GetCollection<IntakeRule>(collections.IntakeRules);
        await intakeRules.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<IntakeRule>(Builders<IntakeRule>.IndexKeys.Ascending(r => r.ProjectId)),
        });

        // Canned Responses
        var cannedResponses = database.GetCollection<CannedResponse>(collections.CannedResponses);
        await cannedResponses.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<CannedResponse>(Builders<CannedResponse>.IndexKeys.Ascending(c => c.ProjectId)),
        });

        // Audit Log
        var auditLog = database.GetCollection<AuditLogEntry>(collections.AuditLog);
        await auditLog.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<AuditLogEntry>(Builders<AuditLogEntry>.IndexKeys.Ascending(a => a.CompanyId)),
            new CreateIndexModel<AuditLogEntry>(Builders<AuditLogEntry>.IndexKeys.Combine(
                Builders<AuditLogEntry>.IndexKeys.Ascending(a => a.EntityType),
                Builders<AuditLogEntry>.IndexKeys.Ascending(a => a.EntityId))),
            new CreateIndexModel<AuditLogEntry>(Builders<AuditLogEntry>.IndexKeys.Descending(a => a.CreatedOnDateTime)),
        });

        // Kanban Boards
        var kanbanBoards = database.GetCollection<KanbanBoard>(collections.KanbanBoards);
        await kanbanBoards.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<KanbanBoard>(Builders<KanbanBoard>.IndexKeys.Ascending(b => b.ProjectId),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<KanbanBoard>(Builders<KanbanBoard>.IndexKeys.Ascending(b => b.CompanyId)),
        });

        // Kanban Cards
        var kanbanCards = database.GetCollection<KanbanCard>(collections.KanbanCards);
        await kanbanCards.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<KanbanCard>(Builders<KanbanCard>.IndexKeys.Ascending(c => c.BoardId)),
            new CreateIndexModel<KanbanCard>(Builders<KanbanCard>.IndexKeys.Combine(
                Builders<KanbanCard>.IndexKeys.Ascending(c => c.BoardId),
                Builders<KanbanCard>.IndexKeys.Ascending(c => c.ColumnId),
                Builders<KanbanCard>.IndexKeys.Ascending(c => c.Position))),
            new CreateIndexModel<KanbanCard>(Builders<KanbanCard>.IndexKeys.Combine(
                Builders<KanbanCard>.IndexKeys.Ascending(c => c.ProjectId),
                Builders<KanbanCard>.IndexKeys.Ascending(c => c.CardNumber))),
            new CreateIndexModel<KanbanCard>(Builders<KanbanCard>.IndexKeys.Ascending(c => c.LinkedTicketIds)),
            new CreateIndexModel<KanbanCard>(Builders<KanbanCard>.IndexKeys.Ascending(c => c.AssignedToUserId)),
        });

        // Kanban Card Comments
        var kanbanCardComments = database.GetCollection<KanbanCardComment>(collections.KanbanCardComments);
        await kanbanCardComments.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<KanbanCardComment>(Builders<KanbanCardComment>.IndexKeys.Ascending(c => c.CardId)),
        });
    }
}
