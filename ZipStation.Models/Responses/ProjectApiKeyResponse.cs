namespace ZipStation.Models.Responses;

public class ProjectApiKeyResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public bool IsRevoked { get; set; }
    public string? CreatedByUserId { get; set; }
    public long CreatedOnDateTime { get; set; }
}

public class ProjectApiKeyCreatedResponse : ProjectApiKeyResponse
{
    public string FullKey { get; set; } = string.Empty;
}
