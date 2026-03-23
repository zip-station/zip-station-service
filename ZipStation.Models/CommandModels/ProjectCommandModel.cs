namespace ZipStation.Models.CommandModels;

public class ProjectCommandModel : BaseCommandModel
{
    public string CompanyId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? LogoUrl { get; set; }

    public string SupportEmailAddress { get; set; } = string.Empty;
}
