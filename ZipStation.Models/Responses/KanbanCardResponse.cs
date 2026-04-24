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
    public string Title { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public KanbanCardType Type { get; set; }
    public TicketPriority Priority { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? AssignedToUserId { get; set; }
    public List<string> LinkedTicketIds { get; set; } = new();
    public long ResolvedOnDateTime { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
}
