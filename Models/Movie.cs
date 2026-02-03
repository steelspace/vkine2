using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public string BackdropUrl { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public class MovieDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("csfd_id")]
    public int? CsfdId { get; set; }

    [BsonElement("tmdb_id")]
    public int? TmdbId { get; set; }

    [BsonElement("title")]
    public string? Title { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("poster_url")]
    public string? PosterUrl { get; set; }

    [BsonElement("backdrop_url")]
    public string? BackdropUrl { get; set; }

    [BsonElement("localized_titles")]
    public LocalizedTitles? LocalizedTitles { get; set; }
}

[BsonIgnoreExtraElements]
public class LocalizedTitles
{
    [BsonElement("Original")]
    public string? Original { get; set; }
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
