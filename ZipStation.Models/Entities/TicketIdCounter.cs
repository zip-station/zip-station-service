using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ZipStation.Models.Entities;

public class TicketIdCounter
{
    [BsonId]
    [BsonElement("_id")]
    public string ProjectId { get; set; } = string.Empty;

    public long CurrentValue { get; set; }
}
