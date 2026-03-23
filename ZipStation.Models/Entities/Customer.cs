using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class Customer : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();

    [BsonIgnoreIfNull]
    public string? Notes { get; set; }

    public bool IsBanned { get; set; }

    public Dictionary<string, string> Properties { get; set; } = new();

    public int OpenTicketCount { get; set; }

    public int ClosedTicketCount { get; set; }

    public int TotalTicketCount { get; set; }
}
