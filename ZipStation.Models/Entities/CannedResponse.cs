using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class CannedResponse : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Title { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string BodyHtml { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? Shortcut { get; set; }

    public int UsageCount { get; set; }

    // CreatedByUserId is now inherited from BaseEntity
}
