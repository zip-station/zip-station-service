using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class ProjectApiKey : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Name { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string KeyHash { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string KeyPrefix { get; set; } = string.Empty;

    public bool IsRevoked { get; set; }

    // CreatedByUserId is now inherited from BaseEntity
}
