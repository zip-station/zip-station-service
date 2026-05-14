namespace ZipStation.Models.Responses;

public class MaxInstructionResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public List<string> Contexts { get; set; } = new();
    public string Source { get; set; } = "manual";
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}

public class MaxExampleReplyResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ReplyText { get; set; } = string.Empty;
    public string? SourceTicketId { get; set; }
    public string? Notes { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}

public class MaxTestConnectionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class MaxSettingsResponse
{
    public bool Enabled { get; set; }
    public bool ApiKeySet { get; set; }
    public string Model { get; set; } = "claude-sonnet-4-6";
    public string? ProjectContext { get; set; }
    public string? ToneGuide { get; set; }
    public string? ToneAvoid { get; set; }
    public bool AutoSendEnabled { get; set; }
    public double AutoSendThreshold { get; set; } = 0.95;
    public List<string> AutoSendCategories { get; set; } = new();
}
