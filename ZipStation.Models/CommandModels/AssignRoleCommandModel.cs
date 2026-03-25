namespace ZipStation.Models.CommandModels;

public class AssignRoleCommandModel
{
    public string RoleId { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
}
