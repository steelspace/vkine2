using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace vkine.Models;

/// <summary>
/// Represents a movie document as stored in MongoDB.
/// </summary>
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

    // Optional cast, crew & directors arrays in the Mongo document (may not exist for all docs)
    [BsonElement("cast")]
    public List<string>? Cast { get; set; }

    [BsonElement("crew")]
    public List<string>? Crew { get; set; }

    [BsonElement("directors")]
    public List<string>? Directors { get; set; }

    [BsonElement("localized_titles")]
    public LocalizedTitles? LocalizedTitles { get; set; }

    [BsonElement("origin")]
    public string? Origin { get; set; }

    [BsonElement("origin_countries")]
    public List<string>? OriginCountries { get; set; }
}
