using ZipStation.Models.Enums;

namespace ZipStation.Models.Responses;

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string FirebaseUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public List<CompanyMembershipResponse> CompanyMemberships { get; set; } = new();
    public List<ProjectMembershipResponse> ProjectMemberships { get; set; } = new();
    public UserPreferencesResponse Preferences { get; set; } = new();
    public bool IsDisabled { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}

public class UserPreferencesResponse
{
    public string? PreferredLanguage { get; set; }
    public string? Timezone { get; set; }
}

public class CompanyMembershipResponse
{
    public string CompanyId { get; set; } = string.Empty;
    public CompanyRole Role { get; set; }
}

public class ProjectMembershipResponse
{
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
}
