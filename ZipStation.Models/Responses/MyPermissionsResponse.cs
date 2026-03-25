namespace ZipStation.Models.Responses;

public class MyPermissionsResponse
{
    public bool IsOwner { get; set; }
    public List<string> Permissions { get; set; } = new();
}
