using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class Alert : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Name { get; set; } = string.Empty;

    public AlertTriggerType TriggerType { get; set; }

    [BsonIgnoreIfNull]
    public string? TriggerValue { get; set; }

    public AlertChannelType ChannelType { get; set; }

    public string WebhookUrl { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? CustomPayloadTemplate { get; set; }

    public bool IsEnabled { get; set; } = true;
}

public enum AlertTriggerType
{
    NewTicket,
    TicketStatusChange,
    HighSpamScore,
    KeywordInSubject,
    KeywordInBody,
    CustomerContact
}

public enum AlertChannelType
{
    Slack,
    Discord,
    GenericWebhook
}
