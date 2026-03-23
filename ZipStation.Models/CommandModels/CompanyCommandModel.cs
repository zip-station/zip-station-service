namespace ZipStation.Models.CommandModels;

public class CompanyCommandModel : BaseCommandModel
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public string? Domain { get; set; }
}
