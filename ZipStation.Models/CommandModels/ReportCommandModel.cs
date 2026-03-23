using ZipStation.Models.Entities;

namespace ZipStation.Models.CommandModels;

public class ReportCommandModel : BaseCommandModel
{
    public string CompanyId { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ReportFrequency Frequency { get; set; } = ReportFrequency.Weekly;
    public List<string> RecipientEmails { get; set; } = new();
    public bool IncludeTicketSummary { get; set; } = true;
    public bool IncludeResponseTimes { get; set; } = true;
    public bool IncludeAgentPerformance { get; set; } = true;
    public bool IncludeCustomerActivity { get; set; }
    public bool IsEnabled { get; set; } = true;
    public long LastSentOn { get; set; }
    public string? CreatedByUserId { get; set; }
}
