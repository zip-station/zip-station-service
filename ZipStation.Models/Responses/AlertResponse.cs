using ZipStation.Models.Entities;

namespace ZipStation.Models.Responses;

public class AlertResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AlertTriggerType TriggerType { get; set; }
    public string? TriggerValue { get; set; }
    public AlertChannelType ChannelType { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string? CustomPayloadTemplate { get; set; }
    public bool IsEnabled { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}
