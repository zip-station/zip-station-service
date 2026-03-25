namespace ZipStation.Models.CommandModels;

public class InviteUserCommandModel
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public List<string>? ProjectIds { get; set; }
}
