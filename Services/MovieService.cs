using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using vkine.Models;
using vkine.Services.Caching;
using vkine.Services.Search;

namespace vkine.Services;

public class MovieService : IMovieService
{
    private readonly IMongoCollection<MovieDocument> _moviesCollection;
    private readonly IMongoCollection<Schedule> _schedulesCollection;
    private readonly PerformanceCache _performanceCache;
    private readonly IScheduleSearchService _scheduleSearchService;
    private readonly IMemoryCache _memoryCache;

    public MovieService(IMongoDatabase database, IScheduleSearchService scheduleSearchService, IMemoryCache memoryCache)
    {
        _moviesCollection = database.GetCollection<MovieDocument>("movies");
        _schedulesCollection = database.GetCollection<Schedule>("schedule");
        var venuesCollection = database.GetCollection<Venue>("venues");
        _performanceCache = new PerformanceCache(_schedulesCollection, venuesCollection);
        _scheduleSearchService = scheduleSearchService;
        _memoryCache = memoryCache;
    }

    public async Task<List<Movie>> GetMovies(int startIndex, int count)
    {
        if (count <= 0)
        {
            return new List<Movie>();
        }

        var indexList = Enumerable.Range(startIndex, count).ToList();

        if (indexList.Count == 0)
        {
            return new List<Movie>();
        }

        var moviesByIndex = new Dictionary<int, Movie>(indexList.Count);
        var missingIndexes = new List<int>();

        foreach (var index in indexList)
        {
            if (_memoryCache.TryGetValue(GetMovieCacheKey(index), out Movie? cached) && cached is not null)
            {
                moviesByIndex[index] = cached;
            }
            else
            {
                missingIndexes.Add(index);
            }
        }

        if (missingIndexes.Count > 0)
        {
            foreach (var (rangeStart, rangeLength) in BuildRanges(missingIndexes))
            {
                var documents = await _moviesCollection.Find(_ => true)
                    .Skip(rangeStart)
                    .Limit(rangeLength)
                    .ToListAsync();

                if (documents.Count == 0)
                {
                    continue;
                }

                var mapped = documents
                    .Select(document => MapMovieDocument(document))
                    .ToList();

                for (var offset = 0; offset < mapped.Count; offset++)
                {
                    var index = rangeStart + offset;
                    var movie = mapped[offset];
                    moviesByIndex[index] = movie;
                    _memoryCache.Set(GetMovieCacheKey(index), movie, new MemoryCacheEntryOptions
                    {
                        Size = 1
                    });
                    if (movie.Id > 0)
                    {
                        CacheMovieById(movie.Id, movie);
                    }
                }
            }
        }

        var ordered = new List<Movie>(indexList.Count);

        foreach (var index in indexList)
        {
            if (moviesByIndex.TryGetValue(index, out var movie))
            {
                ordered.Add(movie);
            }
        }

        return ordered;
    }

    public async Task<int> GetTotalMovieCount()
    {
        return (int)await _moviesCollection.CountDocumentsAsync(_ => true);
    }

    public async Task<List<Schedule>> GetTodaysSchedules()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var schedules = await _performanceCache.GetAsync(today);
        return schedules;
    }

    public async Task<List<Schedule>> SearchTodaysSchedules(string query)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var schedules = await _performanceCache.GetAsync(today);

        if (string.IsNullOrWhiteSpace(query))
        {
            return schedules;
        }

        return _scheduleSearchService.FilterByQuery(schedules, query);
    }

    public async Task<Dictionary<int, Movie>> GetMoviesByIdsAsync(IEnumerable<int> ids)
    {
        if (ids == null)
        {
            return new Dictionary<int, Movie>();
        }

        var candidates = ids.Where(id => id > 0).Distinct().ToList();

        if (candidates.Count == 0)
        {
            return new Dictionary<int, Movie>();
        }

        var result = new Dictionary<int, Movie>(candidates.Count);
        var missing = new List<int>();

        foreach (var id in candidates)
        {
            if (_memoryCache.TryGetValue(GetMovieByIdCacheKey(id), out Movie? cached) && cached is not null)
            {
                result[id] = cached;
            }
            else
            {
                missing.Add(id);
            }
        }

        if (missing.Count > 0)
        {
            var missingSet = new HashSet<int>(missing);
            var csfdFilter = Builders<MovieDocument>.Filter.In(d => d.CsfdId, missing.Select(id => (int?)id));
            var tmdbFilter = Builders<MovieDocument>.Filter.In(d => d.TmdbId, missing.Select(id => (int?)id));
            var filter = Builders<MovieDocument>.Filter.Or(csfdFilter, tmdbFilter);

            var documents = await _moviesCollection.Find(filter).ToListAsync();

            foreach (var document in documents)
            {
                if (document.CsfdId.HasValue && missingSet.Remove(document.CsfdId.Value))
                {
                    var movie = MapMovieDocument(document, document.CsfdId.Value);
                    result[document.CsfdId.Value] = movie;
                    CacheMovieById(document.CsfdId.Value, movie);
                }

                if (document.TmdbId.HasValue && missingSet.Remove(document.TmdbId.Value))
                {
                    var movie = MapMovieDocument(document, document.TmdbId.Value);
                    result[document.TmdbId.Value] = movie;
                    CacheMovieById(document.TmdbId.Value, movie);
                }
            }
        }

        return result;
    }

    public void InvalidatePerformanceCache()
    {
        _performanceCache.Clear();
    }

    private static IEnumerable<(int start, int length)> BuildRanges(List<int> indexes)
    {
        if (indexes.Count == 0)
        {
            yield break;
        }

        var start = indexes[0];
        var length = 1;

        for (var i = 1; i < indexes.Count; i++)
        {
            var current = indexes[i];
            var previous = indexes[i - 1];

            if (current == previous + 1)
            {
                length++;
            }
            else
            {
                yield return (start, length);
                start = current;
                length = 1;
            }
        }

        yield return (start, length);
    }

    private static Movie MapMovieDocument(MovieDocument document, int? forcedId = null)
    {
        return new Movie
        {
            Id = forcedId ?? document.CsfdId ?? document.TmdbId ?? 0,
            Title = !string.IsNullOrEmpty(document.Title) ? document.Title : document.LocalizedTitles?.Original ?? string.Empty,
            Synopsis = document.Description ?? string.Empty,
            CoverUrl = document.PosterUrl ?? string.Empty,
            BackdropUrl = document.BackdropUrl ?? string.Empty,
            OriginCountries = ResolveOriginCountries(document)
        };
    }

    private void CacheMovieById(int movieId, Movie movie)
    {
        if (movieId <= 0)
        {
            return;
        }

        _memoryCache.Set(GetMovieByIdCacheKey(movieId), movie, new MemoryCacheEntryOptions
        {
            Size = 1
        });
    }

    private static string GetMovieCacheKey(int index) => $"movie-index-{index}";
    private static string GetMovieByIdCacheKey(int id) => $"movie-id-{id}";

    private static List<string> ResolveOriginCountries(MovieDocument document)
    {
        if (document.OriginCountries != null && document.OriginCountries.Count > 0)
        {
            return document.OriginCountries
                .Where(country => !string.IsNullOrWhiteSpace(country))
                .Select(country => country.Trim())
                .Where(country => country.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

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
