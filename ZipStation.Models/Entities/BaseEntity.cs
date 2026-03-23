using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class BaseEntity
{
    [BsonId]
    [BsonElement("_id")]
    [BsonRepresentation(BsonType.String)]
    [DoNotChangeOnPatch]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public long UpdatedOnDateTime { get; set; }

    public long CreatedOnDateTime { get; set; }

    public bool IsVoid { get; set; }

    [BsonIgnoreIfNull]
    public double? TextMatchScore { get; set; }

    [BsonIgnoreIfNull]
    public string? CreatedByUserId { get; set; }

    [BsonIgnoreIfNull]
    public string? UpdatedByUserId { get; set; }
}
