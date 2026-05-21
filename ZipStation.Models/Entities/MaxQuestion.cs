using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class MaxQuestion : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? SourceTicketId { get; set; }

    [BsonIgnoreIfNull]
    public string? SourceStoryId { get; set; }

    [DoNotClearOnPatch]
    public string Question { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? ContextExcerpt { get; set; }

    /// <summary>
    /// pending | answered | dismissed
    /// </summary>
    public string Status { get; set; } = "pending";

    [BsonIgnoreIfNull]
    public string? Answer { get; set; }

    public bool PromotedToContext { get; set; }

    [BsonIgnoreIfNull]
    public long? AnsweredOnDateTime { get; set; }
}
