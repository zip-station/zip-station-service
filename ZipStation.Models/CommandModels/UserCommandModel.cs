namespace ZipStation.Models.CommandModels;

public class UserCommandModel : BaseCommandModel
{
    public string FirebaseUserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }
}
