namespace ZipStation.Models.Responses;

public class CustomerResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string? Notes { get; set; }
    public bool IsBanned { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
    public int OpenTicketCount { get; set; }
    public int ClosedTicketCount { get; set; }
    public int TotalTicketCount { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}
