using ZipStation.Models.Enums;

namespace ZipStation.Models.CommandModels;

public class TicketCommandModel : BaseCommandModel
{
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public string? AssignedToUserId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public List<string> Tags { get; set; } = new();
}
