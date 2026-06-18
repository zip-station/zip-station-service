using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;
using ZipStation.Models.Constants;
using ZipStation.Models.Enums;
using ZipStation.Models.Serialization;

namespace ZipStation.Models.Entities;

public class KanbanCard : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string BoardId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public long CardNumber { get; set; }

    public string ColumnId { get; set; } = string.Empty;

    public double Position { get; set; }

    [DoNotClearOnPatch]
    public string Title { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? DescriptionHtml { get; set; }

    /// A built-in story-type name (see <see cref="KanbanCardTypes"/>) or the id of a custom type
    /// defined on the project's board. Stored as a string; the serializer tolerates legacy int
    /// values from before custom types existed.
    [BsonSerializer(typeof(LegacyCardTypeStringSerializer))]
    public string Type { get; set; } = KanbanCardTypes.Feature;

    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    public List<string> Tags { get; set; } = new();

    [BsonIgnoreIfNull]
    public string? AssignedToUserId { get; set; }

    public List<string> LinkedTicketIds { get; set; } = new();

    /// Sibling of LinkedTicketIds for card→card links. Populated manually by the maintainer
    /// AND appended automatically by Max enrichment when it spots related stories.
    public List<string> LinkedStoryIds { get; set; } = new();

    public long ResolvedOnDateTime { get; set; }

    public List<KanbanCardExternalSource> ExternalSources { get; set; } = new();
}

public class KanbanCardExternalSource
{
    public ExternalSourceType Type { get; set; } = ExternalSourceType.Discord;

    /// Stable, dereferenceable URL — pre-built so the SPA just renders an &lt;a&gt;.
    public string Url { get; set; } = string.Empty;

    public string? GuildId { get; set; }

    public string? ChannelId { get; set; }

    public string? ThreadId { get; set; }

    public string? MessageId { get; set; }

    public string? ThreadTitle { get; set; }

    public List<string> ForumTags { get; set; } = new();

    public string? AuthorName { get; set; }

    public string? AuthorExternalId { get; set; }
}
