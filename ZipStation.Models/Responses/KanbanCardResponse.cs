using ZipStation.Models.Enums;

namespace ZipStation.Models.Responses;

public class KanbanCardResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public long CardNumber { get; set; }
    public string ColumnId { get; set; } = string.Empty;
    public double Position { get; set; }
    public KanbanStoryStatus Status { get; set; }
    public double BacklogPosition { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public string Type { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? AssignedToUserId { get; set; }
    public List<string> LinkedTicketIds { get; set; } = new();
    public List<string> LinkedStoryIds { get; set; } = new();
    public long ResolvedOnDateTime { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public List<KanbanCardExternalSourceResponse> ExternalSources { get; set; } = new();
}

public class KanbanCardExternalSourceResponse
{
    public ExternalSourceType Type { get; set; }
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
