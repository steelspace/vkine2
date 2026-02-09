using vkine.Models;

namespace vkine.Mappers;

/// <summary>
/// Handles mapping between MovieDocument (MongoDB) and Movie (domain model).
/// </summary>
public class MovieMapper
{
    /// <summary>
    /// Maps a MovieDocument to a Movie domain model.
    /// </summary>
    /// <param name="document">The MongoDB document to map.</param>
    /// <returns>A Movie instance.</returns>
    public Movie Map(MovieDocument document)
    {
        return new Movie
        {
            Id = document.CsfdId ?? 0,
            Title = ResolveTitle(document),
            Synopsis = document.Description ?? string.Empty,
            CoverUrl = document.PosterUrl ?? string.Empty,
            BackdropUrl = document.BackdropUrl ?? string.Empty,
            OriginCountries = ParseOriginCountries(document),
            Cast = document.Cast ?? new List<string>(),
            Crew = document.Crew ?? new List<string>(),
            Directors = document.Directors ?? new List<string>()
        }; 
    }

    /// <summary>
    /// Resolves the best available title from a movie document.
    /// </summary>
    /// <param name="document">The movie document.</param>
    /// <returns>The title string, preferring direct title over localized original.</returns>
    private static string ResolveTitle(MovieDocument document)
    {
        if (!string.IsNullOrEmpty(document.Title))
        {
            return document.Title;
        }

        return document.LocalizedTitles?.Original ?? string.Empty;
    }

    /// <summary>
    /// Parses origin countries from a movie document.
    /// Handles both array format and comma-separated string format.
    /// </summary>
    /// <param name="document">The movie document.</param>
    /// <returns>List of unique, trimmed country names.</returns>
    private static List<string> ParseOriginCountries(MovieDocument document)
    {
        // Prefer structured array data
        if (document.OriginCountries != null && document.OriginCountries.Count > 0)
        {
            return document.OriginCountries
                .Where(country => !string.IsNullOrWhiteSpace(country))
                .Select(country => country.Trim())
                .Where(country => country.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Fallback to parsing comma-separated string
        if (!string.IsNullOrWhiteSpace(document.Origin))
        {
            return document.Origin
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(country => country.Trim())
                .Where(country => country.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new List<string>();
    }
}
