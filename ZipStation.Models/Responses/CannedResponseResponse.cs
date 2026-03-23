namespace ZipStation.Models.Responses;

public class CannedResponseResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? Shortcut { get; set; }
    public int UsageCount { get; set; }
    public string? CreatedByUserId { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}
