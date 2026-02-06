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
} 
