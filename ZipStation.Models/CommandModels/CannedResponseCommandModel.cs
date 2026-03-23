namespace ZipStation.Models.CommandModels;

public class CannedResponseCommandModel : BaseCommandModel
{
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? Shortcut { get; set; }
}
