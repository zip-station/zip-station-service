using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;
using ZipStation.Models.Enums;

namespace ZipStation.Models.Entities;

public class Ticket : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public long TicketNumber { get; set; }

    [DoNotClearOnPatch]
    public string Subject { get; set; } = string.Empty;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    [BsonIgnoreIfNull]
    public string? AssignedToUserId { get; set; }

    [BsonIgnoreIfNull]
    public string? CustomerName { get; set; }

    [BsonIgnoreIfNull]
    public string? CustomerEmail { get; set; }

    public List<string> Tags { get; set; } = new();

    public TicketCreationSource CreationSource { get; set; } = TicketCreationSource.Manual;

    public List<string> LinkedTicketIds { get; set; } = new();

    [BsonIgnoreIfNull]
    public string? MergedIntoTicketId { get; set; }
}
