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
}
