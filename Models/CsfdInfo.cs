using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

/// <summary>
/// Represents movie information from CSFD (Czech-Slovak Film Database).
/// </summary>
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
