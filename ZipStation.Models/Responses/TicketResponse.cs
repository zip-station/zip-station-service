using ZipStation.Models.Entities;
using ZipStation.Models.Enums;

namespace ZipStation.Models.Responses;

public class TicketResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public long TicketNumber { get; set; }
    public string Subject { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public List<string> Tags { get; set; } = new();
    public TicketCreationSource CreationSource { get; set; }
    public List<string> LinkedTicketIds { get; set; } = new();
    public string? MergedIntoTicketId { get; set; }
    public MessageSource? LastMessageSource { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}
