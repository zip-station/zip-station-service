using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class AuditLogEntry : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? ProjectId { get; set; }

    [DoNotChangeOnPatch]
    public string Action { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string EntityType { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? EntityId { get; set; }

    [DoNotChangeOnPatch]
    public string UserId { get; set; } = string.Empty;

    public string UserDisplayName { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? Details { get; set; }
}
