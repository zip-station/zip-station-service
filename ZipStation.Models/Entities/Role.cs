namespace ZipStation.Models.Entities;

public class Role : BaseEntity
{
    public string CompanyId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// System roles (e.g. Owner) cannot be edited or deleted.
    /// </summary>
    public bool IsSystem { get; set; }
}
