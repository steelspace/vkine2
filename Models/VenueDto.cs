using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

public class VenueDto
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("venue_id")]
    public int VenueId { get; set; }

    [BsonElement("city")]
    public string? City { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("detail_url")]
    public string? DetailUrl { get; set; }

    [BsonElement("address")]
    public string? Address { get; set; }

    [BsonElement("map_url")]
    public string? MapUrl { get; set; }
}
