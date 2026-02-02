using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

[BsonIgnoreExtraElements]
public class ScheduleDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("movie_id")]
    public int MovieId { get; set; }

    [BsonElement("date")]
    public DateTime Date { get; set; }

    [BsonElement("movie_title")]
    public string MovieTitle { get; set; } = string.Empty;

    [BsonElement("performances")]
    public List<Performance> Performances { get; set; } = new();

    [BsonElement("stored_at")]
    public DateTime StoredAt { get; set; }
}

[BsonIgnoreExtraElements]
public class Performance
{
    [BsonElement("venue_id")]
    public int VenueId { get; set; }

    [BsonElement("showtimes")]
    public List<Showtime> Showtimes { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class Showtime
{
    [BsonElement("start_at")]
    public DateTime StartAt { get; set; }

    [BsonElement("tickets_available")]
    public bool TicketsAvailable { get; set; }

    [BsonElement("ticket_url")]
    public string? TicketUrl { get; set; }

    [BsonElement("is_past")]
    public bool IsPast { get; set; }

    [BsonElement("badges")]
    public List<Badge> Badges { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class Badge
{
    [BsonElement("kind")]
    public int Kind { get; set; }

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;
}