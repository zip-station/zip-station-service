using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class MaxExampleReply : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string ReplyText { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? SourceTicketId { get; set; }

    [BsonIgnoreIfNull]
    public string? Notes { get; set; }
}
