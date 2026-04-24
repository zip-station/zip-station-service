using MongoDB.Driver;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;

namespace ZipStation.Business.Repositories;

public interface IKanbanCardRepository : IBaseRepository<KanbanCard>
{
    Task<List<KanbanCard>> GetByBoardIdAsync(string boardId, bool includeArchived, int archiveDays, string resolvedColumnId);
    Task<List<KanbanCard>> SearchAsync(
        string boardId,
        string? text,
        string? columnId,
        string? assignedToUserId,
        KanbanCardType? type,
        List<string>? tags,
        bool? hasLinkedTickets,
        long? createdSince,
        bool includeArchived,
        int archiveDays,
        string resolvedColumnId);
    Task<KanbanCard?> GetByCardNumberAsync(string projectId, long cardNumber);
    Task<List<KanbanCard>> GetByTicketIdAsync(string ticketId);
    Task<double> GetMaxPositionInColumnAsync(string boardId, string columnId);
    Task<bool> AnyInColumnAsync(string boardId, string columnId);
}

public class KanbanCardRepository : BaseRepository<KanbanCard>, IKanbanCardRepository
{
    public KanbanCardRepository(IMongoDatabase database, string collectionName)
        : base(database, collectionName)
    {
    }

    public async Task<List<KanbanCard>> GetByBoardIdAsync(string boardId, bool includeArchived, int archiveDays, string resolvedColumnId)
    {
        var filter = Builders<KanbanCard>.Filter.Eq(c => c.BoardId, boardId)
                   & Builders<KanbanCard>.Filter.Eq(c => c.IsVoid, false);

        filter &= BuildArchiveFilter(includeArchived, archiveDays, resolvedColumnId);

        return await _Collection.Find(filter)
            .SortBy(c => c.ColumnId)
            .ThenBy(c => c.Position)
            .ToListAsync();
    }

    public async Task<List<KanbanCard>> SearchAsync(
        string boardId,
        string? text,
        string? columnId,
        string? assignedToUserId,
        KanbanCardType? type,
        List<string>? tags,
        bool? hasLinkedTickets,
        long? createdSince,
        bool includeArchived,
        int archiveDays,
        string resolvedColumnId)
    {
        var filter = Builders<KanbanCard>.Filter.Eq(c => c.BoardId, boardId)
                   & Builders<KanbanCard>.Filter.Eq(c => c.IsVoid, false);

        if (!string.IsNullOrWhiteSpace(columnId))
            filter &= Builders<KanbanCard>.Filter.Eq(c => c.ColumnId, columnId);

        if (!string.IsNullOrWhiteSpace(assignedToUserId))
        {
            if (assignedToUserId == "unassigned")
                filter &= Builders<KanbanCard>.Filter.Eq(c => c.AssignedToUserId, (string?)null);
            else
                filter &= Builders<KanbanCard>.Filter.Eq(c => c.AssignedToUserId, assignedToUserId);
        }

        if (type.HasValue)
            filter &= Builders<KanbanCard>.Filter.Eq(c => c.Type, type.Value);

        if (tags != null && tags.Count > 0)
            filter &= Builders<KanbanCard>.Filter.AnyIn(c => c.Tags, tags);

        if (hasLinkedTickets.HasValue)
        {
            filter &= hasLinkedTickets.Value
                ? Builders<KanbanCard>.Filter.SizeGt(c => c.LinkedTicketIds, 0)
                : Builders<KanbanCard>.Filter.Size(c => c.LinkedTicketIds, 0);
        }

        if (createdSince.HasValue)
            filter &= Builders<KanbanCard>.Filter.Gte(c => c.CreatedOnDateTime, createdSince.Value);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(
                System.Text.RegularExpressions.Regex.Escape(text.Trim()), "i");
            var textFilters = new List<FilterDefinition<KanbanCard>>
            {
                Builders<KanbanCard>.Filter.Regex(c => c.Title, regex),
                Builders<KanbanCard>.Filter.Regex(c => c.DescriptionHtml, regex),
            };
            if (long.TryParse(text.Trim().TrimStart('#').TrimStart('0'), out var num) && num > 0)
                textFilters.Add(Builders<KanbanCard>.Filter.Eq(c => c.CardNumber, num));
            filter &= Builders<KanbanCard>.Filter.Or(textFilters);
        }

        filter &= BuildArchiveFilter(includeArchived, archiveDays, resolvedColumnId);

        return await _Collection.Find(filter)
            .SortBy(c => c.ColumnId)
            .ThenBy(c => c.Position)
            .ToListAsync();
    }

    public async Task<KanbanCard?> GetByCardNumberAsync(string projectId, long cardNumber)
    {
        var filter = Builders<KanbanCard>.Filter.Eq(c => c.ProjectId, projectId)
                   & Builders<KanbanCard>.Filter.Eq(c => c.CardNumber, cardNumber)
                   & Builders<KanbanCard>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<KanbanCard>> GetByTicketIdAsync(string ticketId)
    {
        var filter = Builders<KanbanCard>.Filter.AnyEq(c => c.LinkedTicketIds, ticketId)
                   & Builders<KanbanCard>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.Find(filter).ToListAsync();
    }

    public async Task<double> GetMaxPositionInColumnAsync(string boardId, string columnId)
    {
        var filter = Builders<KanbanCard>.Filter.Eq(c => c.BoardId, boardId)
                   & Builders<KanbanCard>.Filter.Eq(c => c.ColumnId, columnId)
                   & Builders<KanbanCard>.Filter.Eq(c => c.IsVoid, false);
        var top = await _Collection.Find(filter)
            .SortByDescending(c => c.Position)
            .Limit(1)
            .FirstOrDefaultAsync();
        return top?.Position ?? 0;
    }

    public async Task<bool> AnyInColumnAsync(string boardId, string columnId)
    {
        var filter = Builders<KanbanCard>.Filter.Eq(c => c.BoardId, boardId)
                   & Builders<KanbanCard>.Filter.Eq(c => c.ColumnId, columnId)
                   & Builders<KanbanCard>.Filter.Eq(c => c.IsVoid, false);
        return await _Collection.CountDocumentsAsync(filter) > 0;
    }

    private static FilterDefinition<KanbanCard> BuildArchiveFilter(bool includeArchived, int archiveDays, string resolvedColumnId)
    {
        if (includeArchived || string.IsNullOrEmpty(resolvedColumnId))
            return Builders<KanbanCard>.Filter.Empty;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(archiveDays, 0)).ToUnixTimeMilliseconds();
        var notInResolved = Builders<KanbanCard>.Filter.Ne(c => c.ColumnId, resolvedColumnId);
        var resolvedButRecent = Builders<KanbanCard>.Filter.Eq(c => c.ColumnId, resolvedColumnId)
                              & Builders<KanbanCard>.Filter.Gte(c => c.ResolvedOnDateTime, cutoff);
        return Builders<KanbanCard>.Filter.Or(notInResolved, resolvedButRecent);
    }
}
