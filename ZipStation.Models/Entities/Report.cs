using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class Report : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? ProjectId { get; set; }

    [DoNotClearOnPatch]
    public string Name { get; set; } = string.Empty;

    public ReportFrequency Frequency { get; set; } = ReportFrequency.Weekly;

    public List<string> RecipientEmails { get; set; } = new();

    public bool IncludeTicketSummary { get; set; } = true;

    public bool IncludeResponseTimes { get; set; } = true;

    public bool IncludeAgentPerformance { get; set; } = true;

    public bool IncludeCustomerActivity { get; set; }

    public bool IsEnabled { get; set; } = true;

    public long LastSentOn { get; set; }

    // CreatedByUserId is now inherited from BaseEntity
}

public enum ReportFrequency
{
    Daily,
    Weekly,
    Monthly
}
