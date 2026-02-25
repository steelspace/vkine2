using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

[BsonIgnoreExtraElements]
public class Credit
{
    [BsonElement("tmdb_id")]
    public int? TmdbId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = string.Empty;

    [BsonElement("photo_url")]
    public string? PhotoUrl { get; set; }
}
