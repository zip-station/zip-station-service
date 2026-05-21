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

public class MaxTicketEnrichmentResponse
{
    public string Id { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public string Status { get; set; } = "complete";
    public string Category { get; set; } = "unsure";
    public string Summary { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? DuplicateOfTicketId { get; set; }
    public List<string> RelatedTicketIds { get; set; } = new();
    public string Platform { get; set; } = "unknown";
    public List<string> Tags { get; set; } = new();
    public string SuggestedActionType { get; set; } = "no_action";
    public string? SuggestedDraft { get; set; }
    public string? SuggestedNotes { get; set; }
    public string? Reasoning { get; set; }
    public bool FlaggedQuestion { get; set; }
    public string? QuestionId { get; set; }
    public string Model { get; set; } = string.Empty;
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}

public class MaxTaskResponse
{
    public string Id { get; set; } = string.Empty;
    public string TicketId { get; set; } = string.Empty;
    public string? StoryId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public double Confidence { get; set; }
    public MaxTaskDetailsResponse Details { get; set; } = new();
    public long CreatedOnDateTime { get; set; }
    public long? ResolvedOnDateTime { get; set; }
}

public class MaxTaskDetailsResponse
{
    public string? Draft { get; set; }
    public string? Notes { get; set; }
    public string? DuplicateOfTicketId { get; set; }
    public string? SuggestedTitle { get; set; }
    public string? SuggestedKanbanType { get; set; }
    public string? QuestionId { get; set; }
    public long? LinkToStoryCardNumber { get; set; }
    public string? LinkToStoryTitle { get; set; }
    public string? DuplicateOfStoryId { get; set; }
    public long? DuplicateOfStoryCardNumber { get; set; }
    public string? DuplicateOfStoryTitle { get; set; }
}

public class MaxQuestionResponse
{
    public string Id { get; set; } = string.Empty;
    public string? SourceTicketId { get; set; }
    public string? SourceStoryId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? ContextExcerpt { get; set; }
    public string Status { get; set; } = "pending";
    public string? Answer { get; set; }
    public bool PromotedToContext { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long? AnsweredOnDateTime { get; set; }
}

public class TicketMaxResponse
{
    public MaxTicketEnrichmentResponse? Enrichment { get; set; }
    public List<MaxTaskResponse> Tasks { get; set; } = new();
    public List<MaxQuestionResponse> Questions { get; set; } = new();
}

public class MaxStoryEnrichmentResponse
{
    public string Id { get; set; } = string.Empty;
    public string StoryId { get; set; } = string.Empty;
    public string Status { get; set; } = "complete";
    public string Category { get; set; } = "unclear";
    public string Summary { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? DuplicateOfStoryId { get; set; }
    public List<string> RelatedStoryIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string SuggestedActionType { get; set; } = "no_action";
    public string? SuggestedNotes { get; set; }
    public string? Reasoning { get; set; }
    public bool FlaggedQuestion { get; set; }
    public string? QuestionId { get; set; }
    public string Model { get; set; } = string.Empty;
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}

public class StoryMaxResponse
{
    public MaxStoryEnrichmentResponse? Enrichment { get; set; }
    public List<MaxTaskResponse> Tasks { get; set; } = new();
    public List<MaxQuestionResponse> Questions { get; set; } = new();
}

public class MaxTaskWithTicketResponse
{
    public MaxTaskResponse Task { get; set; } = new();
    /// One of (TicketNumber, TicketSubject, CustomerName/Email) — when the underlying task targets a ticket.
    public long? TicketNumber { get; set; }
    public string? TicketSubject { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    /// One of (StoryCardNumber, StoryTitle) — when the underlying task targets a kanban story.
    public long? StoryCardNumber { get; set; }
    public string? StoryTitle { get; set; }
}

public class MaxQuestionWithSourceResponse
{
    public MaxQuestionResponse Question { get; set; } = new();
    /// "ticket" or "story" — distinguishes which side the question came from.
    public string SourceType { get; set; } = string.Empty;
    public long? TicketNumber { get; set; }
    public string? TicketSubject { get; set; }
    public string? CustomerName { get; set; }
    public long? StoryCardNumber { get; set; }
    public string? StoryTitle { get; set; }
}

public class MaxQuestionAnswerRequest
{
    public string Answer { get; set; } = string.Empty;
    public bool PromoteToContext { get; set; }
}

public class MaxToneAnalyzerResponse
{
    public string? ToneGuide { get; set; }
    public string? ToneAvoid { get; set; }
    public List<int> RecommendedExampleIndices { get; set; } = new();
    public List<string> Replies { get; set; } = new();
}
