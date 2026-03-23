using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ZipStation.Models.Entities;

public class ResponseTimeStat : BaseEntity
{
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? AgentUserId { get; set; }

    [BsonIgnoreIfNull]
    public string? AgentDisplayName { get; set; }

    public double AvgResponseTimeMinutes { get; set; }
    public double MedianResponseTimeMinutes { get; set; }
    public int TicketsHandled { get; set; }
    public int MessagesCount { get; set; }
    public long ComputedOnDateTime { get; set; }
    public string Period { get; set; } = "daily"; // daily, weekly, monthly
}
