using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

[BsonIgnoreExtraElements]
public class MovieDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("csfd")]
    public CsfdInfo? Csfd { get; set; }
}

[BsonIgnoreExtraElements]
public class CsfdInfo
{
    [BsonElement("id")]
    public int Id { get; set; }

    [BsonElement("czechName")]
    public string? CzechName { get; set; }

    [BsonElement("originalName")]
    public string? OriginalName { get; set; }

    [BsonElement("plot")]
    public string? Plot { get; set; }

    [BsonElement("posterUrl")]
    public string? PosterUrl { get; set; }
}
