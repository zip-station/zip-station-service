namespace ZipStation.Models.CommandModels;

public class CompleteInviteCommandModel
{
    public string FirebaseUserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
