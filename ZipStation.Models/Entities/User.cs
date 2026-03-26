using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

[BsonIgnoreExtraElements]
public class User : BaseEntity
{
    [DoNotChangeOnPatch]
    public string FirebaseUserId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Role assignments — each entry assigns a role either company-wide (projectId=null)
    /// or to a specific project.
    /// </summary>
    public List<RoleAssignment> RoleAssignments { get; set; } = new();

    [BsonIgnoreIfNull]
    public string? EmailSignatureHtml { get; set; }

    public UserPreferences Preferences { get; set; } = new();

    public bool IsDisabled { get; set; }

    [BsonIgnoreIfNull]
    public string? InviteCode { get; set; }

    public long InviteCodeExpiresOn { get; set; }
}

public class RoleAssignment
{
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>
    /// The role ID from the roles collection.
    /// </summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>
    /// If null, this is a company-wide assignment (applies to all projects).
    /// If set, this role only applies to the specified project.
    /// </summary>
    [BsonIgnoreIfNull]
    public string? ProjectId { get; set; }
}

public class UserPreferences
{
    public string? PreferredLanguage { get; set; }
    public string? Timezone { get; set; }
}
