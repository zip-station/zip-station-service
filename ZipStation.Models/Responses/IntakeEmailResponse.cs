using ZipStation.Models.Enums;

namespace ZipStation.Models.Responses;

public class IntakeEmailResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }
    public long ReceivedOn { get; set; }
    public IntakeStatus Status { get; set; }
    public int SpamScore { get; set; }
    public bool DeniedPermanently { get; set; }
    public string? ApprovedByUserId { get; set; }
    public string? DeniedByUserId { get; set; }
    public long ProcessedOn { get; set; }
    public string? TicketId { get; set; }
    public string? MessageId { get; set; }
    public string? InReplyTo { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}
