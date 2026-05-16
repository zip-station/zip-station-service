namespace ZipStation.Models.Responses;

public class PersonalAccessTokenResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TokenPrefix { get; set; } = string.Empty;
    public bool IsRevoked { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long LastUsedOnDateTime { get; set; }
    public long? ExpiresOnDateTime { get; set; }
}

public class PersonalAccessTokenCreatedResponse : PersonalAccessTokenResponse
{
    public string FullToken { get; set; } = string.Empty;
}
