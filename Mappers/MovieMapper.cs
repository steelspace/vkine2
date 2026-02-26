using System.Globalization;
using vkine.Models;
using vkine.Services;

namespace vkine.Mappers;

public class MovieMapper
{
    private readonly ICountryLookupService _lookupService;

    public MovieMapper(ICountryLookupService lookupService)
    {
        _lookupService = lookupService;
    }

    public Movie Map(MovieDocument document)
    {
        var originCodes = ParseOriginCountryCodes(document);

        return new Movie
        {
            Id = document.CsfdId ?? 0,
            TmdbId = document.TmdbId,
            ImdbId = document.ImdbId,
            ImdbRating = document.ImdbRating is > 0 ? document.ImdbRating : null,
            ImdbRatingCount = document.ImdbRatingCount is > 0 ? document.ImdbRatingCount : null,
            Title = ResolveTitle(document),
            TitleEn = ResolveEnglishTitle(document),
            Synopsis = document.Description ?? document.DescriptionCs ?? string.Empty,
            DescriptionCs = document.DescriptionCs ?? document.Description ?? string.Empty,
            DescriptionEn = document.DescriptionEn ?? string.Empty,
            CsfdRating = document.Rating,
            TmdbRating = document.VoteAverage is > 0 ? document.VoteAverage : null,
            CoverUrl = document.PosterUrl ?? string.Empty,
            BackdropUrl = document.BackdropUrl ?? string.Empty,
            Year = document.Year ?? string.Empty,
            Duration = document.Duration ?? string.Empty,
            Genres = document.Genres ?? new List<string>(),
            OriginCountryCodes = originCodes,
            OriginCountries = _lookupService.GetCountryNames(originCodes),
            OriginalLanguage = FormatOriginalLanguage(document.OriginalLanguage),
            Cast = document.Cast ?? new List<string>(),
            Crew = document.Crew ?? new List<string>(),
            Directors = document.Directors ?? new List<string>(),
            Homepage = document.Homepage ?? string.Empty,
            TrailerUrl = document.TrailerUrl,
            Credits = document.Credits ?? new List<Credit>()
        };
    }

    private static string ResolveTitle(MovieDocument document)
    {
        if (!string.IsNullOrEmpty(document.Title))
        {
            return document.Title;
        }

        if (document.LocalizedTitles is not null
            && document.LocalizedTitles.TryGetValue("Original", out var original)
            && !string.IsNullOrEmpty(original))
        {
            return original;
        }

        return string.Empty;
    }

    private static string ResolveEnglishTitle(MovieDocument document)
    {
        if (document.LocalizedTitles is not null)
        {
            // Prefer US, then GB, then any English-region key
            foreach (var key in new[] { "US", "GB", "AU" })
            {
                if (document.LocalizedTitles.TryGetValue(key, out var title) && !string.IsNullOrEmpty(title))
                {
                    return title;
                }
            }
        }

        return string.Empty;
    }

    private List<string> ParseOriginCountryCodes(MovieDocument document)
    {
        if (document.OriginCountryCodes is { Count: > 0 })
        {
            return document.OriginCountryCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(document.Origin))
        {
            return document.Origin
                .Split([',', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Length == 2
                    ? part.ToUpperInvariant()
                    : _lookupService.GetIsoCodeFromCzechName(part))
                .Where(code => !string.IsNullOrEmpty(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()!;
        }

        return [];
    }

    private static string FormatOriginalLanguage(string? originalLanguage)
    {
        if (string.IsNullOrWhiteSpace(originalLanguage))
        {
            return string.Empty;
        }

        var trimmed = originalLanguage.Trim();
        try
        {
            var culture = CultureInfo.GetCultureInfo(trimmed);
            return culture.EnglishName;
        }
        catch (CultureNotFoundException)
        {
            return trimmed.ToUpperInvariant();
        }
    }
}
