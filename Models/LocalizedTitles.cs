using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

/// <summary>
/// Represents localized titles for a movie.
/// </summary>
[BsonIgnoreExtraElements]
public class LocalizedTitles
{
    [BsonElement("original")]
    public string? Original { get; set; }
}
