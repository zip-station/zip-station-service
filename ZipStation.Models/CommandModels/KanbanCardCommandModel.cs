using ZipStation.Models.Enums;

namespace ZipStation.Models.CommandModels;

public class KanbanCardCommandModel : BaseCommandModel
{
    public string ColumnId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public KanbanCardType Type { get; set; } = KanbanCardType.Feature;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public List<string> Tags { get; set; } = new();
    public string? AssignedToUserId { get; set; }
    public List<string> LinkedTicketIds { get; set; } = new();
}
