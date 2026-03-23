using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;
using ZipStation.Models.Enums;

namespace ZipStation.Models.Entities;

public class Company : BaseEntity
{
    [DoNotClearOnPatch]
    public string Name { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Slug { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string OwnerUserId { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? LogoUrl { get; set; }

    [BsonIgnoreIfNull]
    public string? Domain { get; set; }

    [BsonIgnoreIfNull]
    public string? LicenseKey { get; set; }

    [BsonIgnoreIfNull]
    public List<LicenseFeature>? LicensedFeatures { get; set; }

    [BsonIgnoreIfNull]
    public long? LicenseExpiresOn { get; set; }

    public CompanySettings Settings { get; set; } = new();
}

public class CompanySettings
{
    public string DefaultTimezone { get; set; } = "America/New_York";
    public string DefaultLanguage { get; set; } = "en";
}
