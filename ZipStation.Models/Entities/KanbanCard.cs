using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;
using ZipStation.Models.Enums;

namespace ZipStation.Models.Entities;

public class KanbanCard : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string BoardId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public long CardNumber { get; set; }

    public string ColumnId { get; set; } = string.Empty;

    public double Position { get; set; }

    [DoNotClearOnPatch]
    public string Title { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? DescriptionHtml { get; set; }

    public KanbanCardType Type { get; set; } = KanbanCardType.Feature;

    public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    public List<string> Tags { get; set; } = new();

    [BsonIgnoreIfNull]
    public string? AssignedToUserId { get; set; }

    public List<string> LinkedTicketIds { get; set; } = new();

    public long ResolvedOnDateTime { get; set; }
}
