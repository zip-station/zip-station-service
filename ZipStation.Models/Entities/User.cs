using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;
using ZipStation.Models.Enums;

namespace ZipStation.Models.Entities;

public class User : BaseEntity
{
    [DoNotChangeOnPatch]
    public string FirebaseUserId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? AvatarUrl { get; set; }

    public List<CompanyMembership> CompanyMemberships { get; set; } = new();

    public List<ProjectMembership> ProjectMemberships { get; set; } = new();

    [BsonIgnoreIfNull]
    public string? EmailSignatureHtml { get; set; }

    public UserPreferences Preferences { get; set; } = new();

    public bool IsDisabled { get; set; }

    [BsonIgnoreIfNull]
    public string? InviteCode { get; set; }

    public long InviteCodeExpiresOn { get; set; }
}

public class UserPreferences
{
    public string? PreferredLanguage { get; set; }
    public string? Timezone { get; set; }
}

public class CompanyMembership
{
    public string CompanyId { get; set; } = string.Empty;
    public CompanyRole Role { get; set; }
}

public class ProjectMembership
{
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
}
