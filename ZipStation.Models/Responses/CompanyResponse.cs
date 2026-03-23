using ZipStation.Models.Enums;

namespace ZipStation.Models.Responses;

public class CompanyResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Domain { get; set; }
    public List<LicenseFeature>? LicensedFeatures { get; set; }
    public CompanySettingsResponse Settings { get; set; } = new();
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}

public class CompanySettingsResponse
{
    public string DefaultTimezone { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = string.Empty;
}
