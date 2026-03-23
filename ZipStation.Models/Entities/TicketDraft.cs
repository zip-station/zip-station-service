using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class TicketDraft : BaseEntity
{
    [DoNotChangeOnPatch]
    public string TicketId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string UserId { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? BodyHtml { get; set; }

    public bool IsInternalNote { get; set; }
}
