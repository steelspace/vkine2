using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Text.RegularExpressions;
using vkine.Mappers;
using vkine.Models;

namespace vkine.Services;

public class MovieService(
    IMongoDatabase database,
    IMemoryCache memoryCache,
    IScheduleService scheduleService,
    ILogger<MovieService> logger) : IMovieService
{
    private const string MOVIE_CACHE_KEY_PREFIX = "movie-";
    private const string MOVIES_COLLECTION_NAME = "movies";

    private readonly IMongoCollection<MovieDocument> _moviesCollection = database.GetCollection<MovieDocument>(MOVIES_COLLECTION_NAME);
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly IScheduleService _scheduleService = scheduleService;
    private readonly ILogger<MovieService> _logger = logger;
    private readonly MovieMapper _movieMapper = new();

    public async Task<List<Movie>> GetMoviesAsync(int startIndex, int count)
    {
        if (count <= 0)
        {
            return new List<Movie>();
        }

        var documents = await _moviesCollection
            .Find(_ => true)
            .Skip(startIndex)
            .Limit(count)
            .ToListAsync();

        var movies = documents
            .Select(_movieMapper.Map)
            .ToList();

        // Cache by ID for later lookups
        foreach (var movie in movies.Where(m => m.Id > 0))
        {
            CacheMovie(movie);
        }

        return movies;
    }

    public async Task<int> GetTotalMovieCountAsync()
    {
        return (int)await _moviesCollection.CountDocumentsAsync(_ => true);
    }

    public async Task<Dictionary<int, Movie>> GetMoviesByIdsAsync(IEnumerable<int> ids)
    {
        var csfdIds = ids.Where(id => id > 0).Distinct().ToList();

        if (csfdIds.Count == 0)
        {
            return new Dictionary<int, Movie>();
        }

        var result = new Dictionary<int, Movie>();
        var missing = new List<int>();

        // Check cache first
        foreach (var id in csfdIds)
        {
            if (_memoryCache.TryGetValue(GetCacheKey(id), out Movie? cached) && cached != null)
            {
                result[id] = cached;
            }
            else
            {
                missing.Add(id);
            }
        }

        // Fetch missing from database
        if (missing.Count > 0)
        {
            var filter = Builders<MovieDocument>.Filter.In(d => d.CsfdId, missing.Select(id => (int?)id));
            var documents = await _moviesCollection.Find(filter).ToListAsync();

            foreach (var doc in documents)
            {
                if (doc.CsfdId.HasValue)
                {
                    var movie = _movieMapper.Map(doc);
                    result[doc.CsfdId.Value] = movie;
                    CacheMovie(movie);
                }
            }

            var notFound = missing.Except(result.Keys).ToList();
            if (notFound.Count > 0)
            {
                _logger.LogWarning("Movies not found for CSFD IDs: {MissingIds}", string.Join(", ", notFound));
            }
        }

        return result;
    }

    public async Task<List<Movie>> SearchMoviesAsync(string query, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<Movie>();

        // Get movie IDs with upcoming showtimes
        var movieIdsWithShowtimes = await _scheduleService.GetAllMovieIdsWithUpcomingPerformancesAsync();

        if (movieIdsWithShowtimes.Count == 0)
        {
            _logger.LogWarning("No movies with upcoming showtimes found");
            return new List<Movie>();
        }

        // Tokenize query and escape regex characters so user input doesn't break regex
        var tokens = query
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => Regex.Escape(t.Trim()))
            .Where(t => t.Length > 0)
            .ToList();

        if (tokens.Count == 0) return new List<Movie>();

        // For each token create an OR filter that looks for the token in title, description, cast, crew,
        // and ALL localized title variants (dynamic country-keyed subdocument)
        var filtersPerToken = tokens.Select(token =>
        {
            // Build a filter that checks every value inside localized_titles (dynamic keys)
            // using $expr + $objectToArray + $filter + $regexMatch
            var localizedTitlesFilter = new BsonDocumentFilterDefinition<MovieDocument>(
                new BsonDocument("$expr",
                    new BsonDocument("$gt", new BsonArray
                    {
                        new BsonDocument("$size",
                            new BsonDocument("$filter", new BsonDocument
                            {
                                { "input", new BsonDocument("$ifNull", new BsonArray
                                    {
                                        new BsonDocument("$objectToArray", "$localized_titles"),
                                        new BsonArray()
                                    })
                                },
                                { "as", "t" },
                                { "cond", new BsonDocument("$regexMatch", new BsonDocument
                                    {
                                        { "input", "$$t.v" },
                                        { "regex", token },
                                        { "options", "i" }
                                    })
                                }
                            })),
                        0
                    })));

            return Builders<MovieDocument>.Filter.Or(
                Builders<MovieDocument>.Filter.Regex(d => d.Title, new BsonRegularExpression(token, "i")),
                Builders<MovieDocument>.Filter.Regex(d => d.Description, new BsonRegularExpression(token, "i")),
                Builders<MovieDocument>.Filter.Regex("cast", new BsonRegularExpression(token, "i")),
                Builders<MovieDocument>.Filter.Regex("crew", new BsonRegularExpression(token, "i")),
                Builders<MovieDocument>.Filter.Regex("directors", new BsonRegularExpression(token, "i")),
                localizedTitlesFilter
            );
        }).ToList();

        // Add filter to only include movies with upcoming showtimes
        var textSearchFilter = Builders<MovieDocument>.Filter.And(filtersPerToken);
        var showtimeFilter = Builders<MovieDocument>.Filter.In(d => d.CsfdId, movieIdsWithShowtimes.Select(id => (int?)id));
        var finalFilter = Builders<MovieDocument>.Filter.And(textSearchFilter, showtimeFilter);

        var documents = await _moviesCollection.Find(finalFilter).Limit(limit).ToListAsync();

        return documents.Select(_movieMapper.Map).ToList();
    }

    private void CacheMovie(Movie movie)
    {
        if (movie.Id > 0)
        {
            _memoryCache.Set(GetCacheKey(movie.Id), movie, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(30),
                Size = 1
            });
        }
    }

    private static string GetCacheKey(int id) => $"{MOVIE_CACHE_KEY_PREFIX}{id}";
}
