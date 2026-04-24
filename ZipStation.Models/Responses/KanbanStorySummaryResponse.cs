using ZipStation.Models.Enums;

namespace ZipStation.Models.Responses;

public class KanbanStorySummaryResponse
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public long CardNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public KanbanCardType Type { get; set; }
    public TicketPriority Priority { get; set; }
    public string ColumnId { get; set; } = string.Empty;
    public string? ColumnName { get; set; }
    public string? AssignedToUserId { get; set; }
}
