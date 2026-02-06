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

    [BsonElement("czech_name")]
    public string? CzechName { get; set; }

    [BsonElement("original_name")]
    public string? OriginalName { get; set; }

    [BsonElement("plot")]
    public string? Plot { get; set; }

    [BsonElement("poster_url")]
    public string? PosterUrl { get; set; }
}
