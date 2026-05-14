using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class MaxTicketEnrichment : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string TicketId { get; set; } = string.Empty;

    public string Category { get; set; } = "unsure";

    public string Summary { get; set; } = string.Empty;

    public double Confidence { get; set; }

    [BsonIgnoreIfNull]
    public string? DuplicateOfTicketId { get; set; }

    public List<string> RelatedTicketIds { get; set; } = new();

    public string Platform { get; set; } = "unknown";

    public List<string> Tags { get; set; } = new();

    public string SuggestedActionType { get; set; } = "no_action";

    [BsonIgnoreIfNull]
    public string? SuggestedDraft { get; set; }

    [BsonIgnoreIfNull]
    public string? SuggestedNotes { get; set; }

    [BsonIgnoreIfNull]
    public string? Reasoning { get; set; }

    public bool FlaggedQuestion { get; set; }

    [BsonIgnoreIfNull]
    public string? QuestionId { get; set; }

    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Raw JSON Anthropic response, retained for debugging.
    /// </summary>
    [BsonIgnoreIfNull]
    public string? RawResponse { get; set; }
}
