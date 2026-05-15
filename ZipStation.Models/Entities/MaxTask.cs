using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class MaxTask : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string TicketId { get; set; } = string.Empty;

    /// <summary>
    /// draft_reply | merge_duplicate | add_to_backlog | escalated | flagged_question | investigate
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// pending | approved | rejected | auto_executed | failed
    /// </summary>
    public string Status { get; set; } = "pending";

    public double Confidence { get; set; }

    public MaxTaskDetails Details { get; set; } = new();

    [BsonIgnoreIfNull]
    public string? ApprovedByUserId { get; set; }

    [BsonIgnoreIfNull]
    public long? ResolvedOnDateTime { get; set; }

    [BsonIgnoreIfNull]
    public string? FailureReason { get; set; }
}

public class MaxTaskDetails
{
    [BsonIgnoreIfNull]
    public string? Draft { get; set; }

    [BsonIgnoreIfNull]
    public string? Notes { get; set; }

    [BsonIgnoreIfNull]
    public string? DuplicateOfTicketId { get; set; }

    [BsonIgnoreIfNull]
    public string? SuggestedTitle { get; set; }

    [BsonIgnoreIfNull]
    public string? SuggestedKanbanType { get; set; }

    [BsonIgnoreIfNull]
    public string? QuestionId { get; set; }

    /// <summary>
    /// For link_to_story tasks: the kanban card number Max wants to link this ticket to.
    /// </summary>
    [BsonIgnoreIfNull]
    public long? LinkToStoryCardNumber { get; set; }

    /// <summary>
    /// For link_to_story tasks: a human-readable label for the target story (its title).
    /// </summary>
    [BsonIgnoreIfNull]
    public string? LinkToStoryTitle { get; set; }
}
