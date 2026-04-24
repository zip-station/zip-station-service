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
}

public class KanbanColumn
{
    public string Id { get; set; } = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

    public string Name { get; set; } = string.Empty;

    [BsonIgnoreIfNull]
    public string? Color { get; set; }

    public int Position { get; set; }
}
