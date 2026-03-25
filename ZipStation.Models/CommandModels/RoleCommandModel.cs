namespace ZipStation.Models.CommandModels;

public class RoleCommandModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}
