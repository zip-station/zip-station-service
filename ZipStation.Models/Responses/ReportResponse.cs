using ZipStation.Models.Entities;

namespace ZipStation.Models.Responses;

public class ReportResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ReportFrequency Frequency { get; set; }
    public List<string> RecipientEmails { get; set; } = new();
    public bool IncludeTicketSummary { get; set; }
    public bool IncludeResponseTimes { get; set; }
    public bool IncludeAgentPerformance { get; set; }
    public bool IncludeCustomerActivity { get; set; }
    public bool IsEnabled { get; set; }
    public long LastSentOn { get; set; }
    public string? CreatedByUserId { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}
