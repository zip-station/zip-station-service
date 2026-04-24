using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;
using ZipStation.Models.Enums;

namespace ZipStation.Models.Entities;

public class KanbanCardComment : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CardId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    public KanbanCommentType Type { get; set; } = KanbanCommentType.User;

    [BsonIgnoreIfNull]
    public string? AuthorUserId { get; set; }

    [BsonIgnoreIfNull]
    public string? AuthorName { get; set; }

    public string BodyHtml { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public List<MessageAttachment>? Attachments { get; set; }
}
