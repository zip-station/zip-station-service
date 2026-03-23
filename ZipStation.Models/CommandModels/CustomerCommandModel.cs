namespace ZipStation.Models.CommandModels;

public class CustomerCommandModel : BaseCommandModel
{
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? Notes { get; set; }
    public bool IsBanned { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}
