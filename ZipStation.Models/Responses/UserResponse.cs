namespace ZipStation.Models.Responses;

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string FirebaseUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public List<RoleAssignmentResponse> RoleAssignments { get; set; } = new();
    public bool IsOwner { get; set; }
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

public class RoleAssignmentResponse
{
    public string CompanyId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string? RoleName { get; set; }
    public string? ProjectId { get; set; }
}
