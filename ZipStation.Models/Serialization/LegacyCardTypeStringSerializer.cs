using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using ZipStation.Models.Constants;

namespace ZipStation.Models.Serialization;

/// Reads story-type fields that may still be stored as the legacy BSON int
/// (0=Feature, 1=Bug, 2=Improvement, 3=TechDebt) and surfaces them as strings, while writing
/// new values as plain strings. This lets the enum→string migration happen without any window
/// where existing cards (or Discord <c>DefaultCardType</c> settings) fail to deserialize.
///
/// Scoped to specific members via <c>[BsonSerializer(typeof(LegacyCardTypeStringSerializer))]</c>
/// — it is intentionally NOT registered globally, so ordinary string fields are unaffected.
public class LegacyCardTypeStringSerializer : SerializerBase<string?>
{
    public override string? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        switch (reader.GetCurrentBsonType())
        {
            case BsonType.String:
                return reader.ReadString();
            case BsonType.Int32:
                return KanbanCardTypes.FromLegacyInt(reader.ReadInt32()) ?? KanbanCardTypes.Feature;
            case BsonType.Int64:
                return KanbanCardTypes.FromLegacyInt((int)reader.ReadInt64()) ?? KanbanCardTypes.Feature;
            case BsonType.Null:
                reader.ReadNull();
                return null;
            default:
                reader.SkipValue();
                return null;
        }
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string? value)
    {
        if (value == null)
            context.Writer.WriteNull();
        else
            context.Writer.WriteString(value);
    }
}
