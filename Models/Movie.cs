namespace vkine.Models;

/// <summary>
/// Represents a movie domain model.
/// </summary>
public class Movie
{
    public int Id { get; set; }
    public int? TmdbId { get; set; }
    public string? ImdbId { get; set; }
    public double? ImdbRating { get; set; }
    public int? ImdbRatingCount { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string? CsfdRating { get; set; }
    public double? TmdbRating { get; set; }
    public string CoverUrl { get; set; } = string.Empty;
    public string BackdropUrl { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public List<string> OriginCountries { get; set; } = new();
    public List<string> Cast { get; set; } = new();
    public List<string> Crew { get; set; } = new();
    public List<string> Directors { get; set; } = new();
    public string Homepage { get; set; } = string.Empty;
} 
