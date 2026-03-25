namespace ZipStation.Models.Responses;

public class InviteValidationResponse
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
