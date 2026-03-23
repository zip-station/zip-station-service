using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ZipStation.Models.Entities;

public class TicketViewer
{
    [BsonId]
    [BsonElement("_id")]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string TicketId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public long LastHeartbeat { get; set; }
}
