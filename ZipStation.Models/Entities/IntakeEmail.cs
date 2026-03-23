using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;
using ZipStation.Models.Enums;

namespace ZipStation.Models.Entities;

public class IntakeEmail : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Subject { get; set; } = string.Empty;

    public string BodyText { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? BodyHtml { get; set; }

    public long ReceivedOn { get; set; }

    public IntakeStatus Status { get; set; } = IntakeStatus.Pending;

    public int SpamScore { get; set; }

    public bool DeniedPermanently { get; set; }

    [BsonIgnoreIfNull]
    public string? ApprovedByUserId { get; set; }

    [BsonIgnoreIfNull]
    public string? DeniedByUserId { get; set; }

    public long ProcessedOn { get; set; }

    [BsonIgnoreIfNull]
    public string? TicketId { get; set; }

    [BsonIgnoreIfNull]
    public string? MessageId { get; set; }

    [BsonIgnoreIfNull]
    public string? InReplyTo { get; set; }
}
