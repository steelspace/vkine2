using vkine.Models;
using vkine.Services;

namespace vkine.Mappers;

/// <summary>
/// Handles mapping between MovieDocument (MongoDB) and Movie (domain model).
/// </summary>
public class MovieMapper(ICountryLookupService lookupService)
{
    private readonly ICountryLookupService _lookupService = lookupService;

    /// <summary>
    /// Maps a MovieDocument to a Movie domain model.
    /// </summary>
    /// <param name="document">The MongoDB document to map.</param>
    /// <returns>A Movie instance.</returns>
    public Movie Map(MovieDocument document)
    {
        var codes = ParseOriginCountryCodes(document);
        
        return new Movie
        {
            Id = document.CsfdId ?? 0,
            TmdbId = document.TmdbId,
            ImdbId = document.ImdbId,
            ImdbRating = document.ImdbRating is > 0 ? document.ImdbRating : null,
            ImdbRatingCount = document.ImdbRatingCount is > 0 ? document.ImdbRatingCount : null,
            Title = ResolveTitle(document),
            Synopsis = document.Description ?? string.Empty,
            CsfdRating = document.Rating,
            TmdbRating = document.VoteAverage is > 0 ? document.VoteAverage : null,
            CoverUrl = document.PosterUrl ?? string.Empty,
            BackdropUrl = document.BackdropUrl ?? string.Empty,
            Year = document.Year ?? string.Empty,
            Duration = document.Duration ?? string.Empty,
            Genres = document.Genres ?? new List<string>(),
            OriginCountryCodes = codes,
            OriginCountries = _lookupService.GetCountryNames(codes),
            Cast = document.Cast ?? new List<string>(),
            Crew = document.Crew ?? new List<string>(),
            Directors = document.Directors ?? new List<string>(),
            Homepage = document.Homepage ?? string.Empty,
            TrailerUrl = document.TrailerUrl
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
    /// Parses origin country codes from a movie document.
    /// Handles both array format and comma-separated string format.
    /// </summary>
    /// <param name="document">The movie document.</param>
    /// <returns>List of unique, trimmed country codes.</returns>
    private static List<string> ParseOriginCountryCodes(MovieDocument document)
    {
        // Prefer structured array data
        if (document.OriginCountryCodes != null && document.OriginCountryCodes.Count > 0)
        {
            return document.OriginCountryCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Where(code => code.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Fallback to parsing comma-separated string in the old origin field
        if (!string.IsNullOrWhiteSpace(document.Origin))
        {
            return document.Origin
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(code => code.Trim())
                .Where(code => code.Length == 2) // only codes
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new List<string>();
    }
}
