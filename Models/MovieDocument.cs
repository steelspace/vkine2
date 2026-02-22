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

    [BsonElement("imdb_id")]
    public string? ImdbId { get; set; }

    [BsonElement("imdb_rating")]
    public double? ImdbRating { get; set; }

    [BsonElement("imdb_rating_count")]
    public int? ImdbRatingCount { get; set; }

    [BsonElement("rating")]
    public string? Rating { get; set; }

    [BsonElement("vote_average")]
    public double? VoteAverage { get; set; }

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

    [BsonElement("year")]
    public string? Year { get; set; }

    [BsonElement("duration")]
    public string? Duration { get; set; }

    [BsonElement("genres")]
    public List<string>? Genres { get; set; }

    [BsonElement("origin")]
    public string? Origin { get; set; }

    [BsonElement("origin_country_codes")]
    public List<string>? OriginCountryCodes { get; set; }

    [BsonElement("homepage")]
    public string? Homepage { get; set; }

    [BsonElement("trailer_url")]
    public string? TrailerUrl { get; set; }
}
