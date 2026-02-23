using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            Synopsis = document.Description ?? string.Empty,
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
            TrailerUrl = document.TrailerUrl
        };
    }

    private static string ResolveTitle(MovieDocument document)
    {
        if (!string.IsNullOrEmpty(document.Title))
        {
            return document.Title;
        }

        return document.LocalizedTitles?.Original ?? string.Empty;
    }

    private List<string> ParseOriginCountryCodes(MovieDocument document)
    {
        if (document.OriginCountryCodes != null && document.OriginCountryCodes.Count > 0)
        {
            return document.OriginCountryCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Where(code => code.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(document.Origin))
        {
            return document.Origin
                .Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Select(part =>
                {
                    if (part.Length == 2)
                    {
                        return part.ToUpperInvariant();
                    }

                    return _lookupService.GetIsoCodeFromCzechName(part);
                })
                .Where(code => !string.IsNullOrEmpty(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()!;
        }

        return new List<string>();
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
