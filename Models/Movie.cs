namespace vkine.Models;

/// <summary>
/// Represents a movie domain model.
/// </summary>
public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public string BackdropUrl { get; set; } = string.Empty;
    public List<string> OriginCountries { get; set; } = new();

    // New: lists of cast and crew names for richer searching
    public List<string> Cast { get; set; } = new();
    public List<string> Crew { get; set; } = new();

    // New: list of directors for searching
    public List<string> Directors { get; set; } = new();
} 
