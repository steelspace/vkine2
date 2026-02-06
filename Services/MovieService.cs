using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using vkine.Mappers;
using vkine.Models;

namespace vkine.Services;

public class MovieService(
    IMongoDatabase database,
    IMemoryCache memoryCache,
    ILogger<MovieService> logger) : IMovieService
{
    private const string MOVIE_CACHE_KEY_PREFIX = "movie-";
    private const string MOVIES_COLLECTION_NAME = "movies";

    private readonly IMongoCollection<MovieDocument> _moviesCollection = database.GetCollection<MovieDocument>(MOVIES_COLLECTION_NAME);
    private readonly IMemoryCache _memoryCache = memoryCache;
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

    private void CacheMovie(Movie movie)
    {
        if (movie.Id > 0)
        {
            _memoryCache.Set(GetCacheKey(movie.Id), movie, new MemoryCacheEntryOptions { Size = 1 });
        }
    }

    private static string GetCacheKey(int id) => $"{MOVIE_CACHE_KEY_PREFIX}{id}";
}
