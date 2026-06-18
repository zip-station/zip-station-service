using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class KanbanBoard : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    public List<KanbanColumn> Columns { get; set; } = new();

    public string ResolvedColumnId { get; set; } = string.Empty;

    /// Project-specific story types in addition to the built-ins (Feature/Bug/Improvement/
    /// TechDebt). A card references one of these by its <see cref="KanbanCardTypeDefinition.Id"/>.
    public List<KanbanCardTypeDefinition> CustomCardTypes { get; set; } = new();
}

public class KanbanCardTypeDefinition
{
    public string Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

    public string Label { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? Color { get; set; }
}

public class KanbanColumn
{
    public string Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

    public string Name { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? Color { get; set; }

    public int Position { get; set; }
}
