using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class MaxStoryEnrichment : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string StoryId { get; set; } = string.Empty;

    /// <summary>
    /// processing | complete | failed
    /// </summary>
    public string Status { get; set; } = "complete";

    /// <summary>
    /// bug | feature | improvement | tech_debt | unclear
    /// </summary>
    public string Category { get; set; } = "unclear";

    public string Summary { get; set; } = string.Empty;

    public double Confidence { get; set; }

    [BsonIgnoreIfNull]
    public string? DuplicateOfStoryId { get; set; }

    public List<string> RelatedStoryIds { get; set; } = new();

    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// merge_story_duplicate | investigate | escalated | no_action
    /// </summary>
    public string SuggestedActionType { get; set; } = "no_action";

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
