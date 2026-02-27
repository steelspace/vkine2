using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

[BsonIgnoreExtraElements]
public class PremiereDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("csfd_id")]
    public int CsfdId { get; set; }

    [BsonElement("premiere_date")]
    public PremiereDateDocument PremiereDate { get; set; } = new();

    [BsonElement("stored_at")]
    public DateTime StoredAt { get; set; }

    [BsonIgnore]
    public DateOnly PremiereDateOnly => new(PremiereDate.Year, PremiereDate.Month, PremiereDate.Day);
}

[BsonIgnoreExtraElements]
public class PremiereDateDocument
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
}
