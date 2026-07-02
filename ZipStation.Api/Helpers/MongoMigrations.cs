using MongoDB.Driver;
using Serilog;
using ZipStation.Business.Helpers;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;

namespace ZipStation.Api.Helpers;

/// One-off, idempotent data migrations run at startup (after indexes are ensured). Each migration
/// is written so re-running it is a no-op once the data is in the new shape.
public static class MongoMigrations
{
    public static async Task RunAsync(IMongoDatabase database, AppConfig appConfig)
    {
        await BackfillKanbanCardStatusAsync(database, appConfig);
        await ClearOffBoardColumnIdAsync(database, appConfig);
    }

    /// Stories existed before <see cref="KanbanCard.Status"/> did. Those documents have no `status`
    /// field; treat them all as <see cref="KanbanStoryStatus.Committed"/> so they keep showing on
    /// their boards exactly as before. Idempotent: once every card has a status, the filter matches
    /// nothing. (Status filtering in Mongo needs a stored value — the C# default only helps reads.)
    private static async Task BackfillKanbanCardStatusAsync(IMongoDatabase database, AppConfig appConfig)
    {
        var cards = database.GetCollection<KanbanCard>(appConfig.ZipStationMongoDb.Collections.KanbanCards);

        var missingStatus = Builders<KanbanCard>.Filter.Exists(c => c.Status, false);
        var update = Builders<KanbanCard>.Update.Set(c => c.Status, KanbanStoryStatus.Committed);

        var result = await cards.UpdateManyAsync(missingStatus, update);
        if (result.ModifiedCount > 0)
            Log.Information("Migration: set status=Committed on {Count} legacy kanban cards", result.ModifiedCount);
    }

    /// Off-board statuses (Unreviewed / Backlog / Archived / Obsolete) no longer belong to a board
    /// column — <see cref="KanbanStatusRules.PlanStatusChange"/> clears <c>ColumnId</c> on transition
    /// and Discord intake now creates cards column-less. Cards imported / transitioned before that
    /// change still carry a stale column id, so blank it here. Idempotent: once every off-board card
    /// has an empty <c>ColumnId</c>, the filter matches nothing. On-board cards (Committed/Resolved)
    /// are untouched.
    private static async Task ClearOffBoardColumnIdAsync(IMongoDatabase database, AppConfig appConfig)
    {
        var cards = database.GetCollection<KanbanCard>(appConfig.ZipStationMongoDb.Collections.KanbanCards);

        var offBoardStatuses = Enum.GetValues<KanbanStoryStatus>()
            .Where(s => !KanbanStatusRules.IsOnBoard(s))
            .ToArray();

        var filter = Builders<KanbanCard>.Filter.In(c => c.Status, offBoardStatuses)
                   & Builders<KanbanCard>.Filter.Ne(c => c.ColumnId, string.Empty);
        var update = Builders<KanbanCard>.Update.Set(c => c.ColumnId, string.Empty);

        var result = await cards.UpdateManyAsync(filter, update);
        if (result.ModifiedCount > 0)
            Log.Information("Migration: cleared stale ColumnId on {Count} off-board kanban cards", result.ModifiedCount);
    }
}
