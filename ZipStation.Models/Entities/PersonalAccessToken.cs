using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class PersonalAccessToken : BaseEntity
{
    [DoNotChangeOnPatch]
    public string UserId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Name { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string TokenHash { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string TokenPrefix { get; set; } = string.Empty;

    public bool IsRevoked { get; set; }

    public long LastUsedOnDateTime { get; set; }

    [BsonIgnoreIfNull]
    public long? ExpiresOnDateTime { get; set; }
}
