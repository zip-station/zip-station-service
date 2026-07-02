using System.Text.RegularExpressions;
using MongoDB.Driver;
using ZipStation.Models.Entities;
using ZipStation.Models.Enums;

namespace ZipStation.Business.Helpers;

/// Shared logic for "search by external-source link": turning a pasted URL (e.g. a Discord forum
/// post) into a <see cref="KanbanCardExternalSource"/>, and matching that against cards' stored
/// <see cref="KanbanCard.ExternalSources"/>. Kept in one place so every surface that searches —
/// the kanban board, the cross-project backlog grid, the story search — resolves the same link the
/// same way, and so the parser is unit-testable without a database.
public static class ExternalSourceSearch
{
    private static readonly Regex _discordUrlPattern = new(
        @"^https?://(?:[a-z]+\.)?discord(?:app)?\.com/channels/(\d+)/(\d+)(?:/(\d+))?/?(?:\?.*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// Parse a pasted URL into a KanbanCardExternalSource. Returns null when the URL isn't
    /// a recognized source type — caller decides whether to fall back (generic Link / text search).
    public static KanbanCardExternalSource? Parse(string url)
    {
        var m = _discordUrlPattern.Match(url);
        if (!m.Success) return null;

        var guildId = m.Groups[1].Value;
        var second = m.Groups[2].Value;
        var third = m.Groups[3].Success ? m.Groups[3].Value : null;

        // 3-segment = full message URL; 2-segment = thread URL (most common forum case).
        // We can't distinguish a thread from a regular channel without an API call, so we
        // optimistically treat the 2-segment form as a forum thread (where threadId == starter messageId).
        var channelId = third != null ? second : (string?)null;
        var threadId = third != null ? second : second;
        var messageId = third ?? second;

        return new KanbanCardExternalSource
        {
            Type = ExternalSourceType.Discord,
            Url = url,
            GuildId = guildId,
            ChannelId = channelId,
            ThreadId = threadId,
            MessageId = messageId,
            ForumTags = new List<string>(),
        };
    }

    /// Match cards whose ExternalSources contain an entry pointing at the same external
    /// resource. We compare on the identifier segments (message/thread/channel) rather than
    /// the raw URL string, so a thread link, a full message link, and a forum-channel link
    /// that resolve to the same post all match regardless of trailing slashes or subdomain.
    public static FilterDefinition<KanbanCard> MatchFilter(KanbanCardExternalSource source)
    {
        var ids = new[] { source.MessageId, source.ThreadId, source.ChannelId }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        var es = Builders<KanbanCardExternalSource>.Filter;
        var elem = es.Eq(s => s.Type, source.Type);

        if (ids.Count > 0)
        {
            elem &= es.Or(
                es.In(s => s.MessageId, ids),
                es.In(s => s.ThreadId, ids),
                es.In(s => s.ChannelId, ids));
        }

        return Builders<KanbanCard>.Filter.ElemMatch(c => c.ExternalSources, elem);
    }
}
