using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;
using ZipStation.Models.Enums;

namespace ZipStation.Models.Entities;

public class TicketMessage : BaseEntity
{
    [DoNotChangeOnPatch]
    public string TicketId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Body { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? BodyHtml { get; set; }

    public bool IsInternalNote { get; set; }

    [BsonIgnoreIfNull]
    public string? AuthorUserId { get; set; }

    [BsonIgnoreIfNull]
    public string? AuthorName { get; set; }

    [BsonIgnoreIfNull]
    public string? AuthorEmail { get; set; }

    public MessageSource Source { get; set; } = MessageSource.Agent;

    [BsonIgnoreIfNull]
    public string? MessageId { get; set; }

    public MessageSendStatus SendStatus { get; set; } = MessageSendStatus.NotApplicable;

    [BsonIgnoreIfNull]
    public string? SendError { get; set; }

    public long SentOnDateTime { get; set; }
}

public enum MessageSendStatus
{
    NotApplicable,
    Pending,
    Sent,
    Failed
}

public enum MessageSource
{
    Customer,
    Agent,
    System
}
