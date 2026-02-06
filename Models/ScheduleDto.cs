using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

public class ScheduleDto
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("date")]
    public DateTime Date { get; set; }

    [BsonElement("movie_id")]
    public int MovieId { get; set; }

    [BsonElement("movie_title")]
    public string? MovieTitle { get; set; }

    [BsonElement("performances")]
    public List<PerformanceDto> Performances { get; set; } = new();

    [BsonElement("stored_at")]
    public DateTime StoredAt { get; set; }
}

public class PerformanceDto
{
    [BsonElement("venue_id")]
    public int VenueId { get; set; }

    [BsonElement("showtimes")]
    public List<ShowtimeDto> Showtimes { get; set; } = new();
}

public class ShowtimeDto
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
    public List<CinemaBadgeDto> Badges { get; set; } = new();
}

public class CinemaBadgeDto
{
    [BsonElement("kind")]
    public BadgeKind Kind { get; set; }

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }
}

public enum BadgeKind
{
    Unknown = 0,
    Technology = 1,
    Format = 2,
    Language = 3
}
