using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

[BsonIgnoreExtraElements]
public class Venue
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("venue_id")]
    public int VenueId { get; set; }

    [BsonElement("address")]
    public string Address { get; set; } = string.Empty;

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("detail_url")]
    public string DetailUrl { get; set; } = string.Empty;

    [BsonElement("map_url")]
    public string MapUrl { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
}