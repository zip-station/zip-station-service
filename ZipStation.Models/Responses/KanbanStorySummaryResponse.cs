using ZipStation.Models.Enums;

namespace ZipStation.Models.Responses;

public class KanbanStorySummaryResponse
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public long CardNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; }
    public string ColumnId { get; set; } = string.Empty;
    public string? ColumnName { get; set; }
    public bool IsResolved { get; set; }
    public string? AssignedToUserId { get; set; }
    public KanbanStoryStatus Status { get; set; }
    public double BacklogPosition { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<KanbanCardExternalSourceResponse> ExternalSources { get; set; } = new();
    public long UpdatedOnDateTime { get; set; }
    public long CreatedOnDateTime { get; set; }
    public string BoardId { get; set; } = string.Empty;
}
