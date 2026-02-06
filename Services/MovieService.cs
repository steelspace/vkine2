using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using vkine.Mappers;
using vkine.Models;
using vkine.Utilities;

namespace vkine.Services;

public class MovieService : IMovieService
{
    private const string MOVIE_INDEX_CACHE_KEY_PREFIX = "movie-index-";
    private const string MOVIE_ID_CACHE_KEY_PREFIX = "movie-id-";
    private const string MOVIES_COLLECTION_NAME = "movies";

    private readonly IMongoCollection<MovieDocument> _moviesCollection;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MovieService> _logger;
    private readonly MovieMapper _movieMapper;

    public MovieService(
        IMongoDatabase database,
        IMemoryCache memoryCache,
        ILogger<MovieService> logger)
    {
        _moviesCollection = database.GetCollection<MovieDocument>(MOVIES_COLLECTION_NAME);
        _memoryCache = memoryCache;
        _logger = logger;
        _movieMapper = new MovieMapper();
    }

    public async Task<List<Movie>> GetMoviesAsync(int startIndex, int count)
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
            _logger.LogDebug("Cache miss for {Count} movie indexes. Fetching from database.", missingIndexes.Count);

            foreach (var (rangeStart, rangeLength) in IndexRangeBuilder.BuildRanges(missingIndexes))
            {
                _logger.LogTrace("Fetching movie range: start={Start}, length={Length}", rangeStart, rangeLength);

                var documents = await _moviesCollection.Find(_ => true)
                    .Skip(rangeStart)
                    .Limit(rangeLength)
                    .ToListAsync();

                if (documents.Count == 0)
                {
                    _logger.LogWarning("No documents found for range: start={Start}, length={Length}", rangeStart, rangeLength);
                    continue;
                }

                var mapped = documents
                    .Select(document => _movieMapper.Map(document))
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

            _logger.LogDebug("Successfully fetched and cached {Count} movies", moviesByIndex.Count);
        }
        else
        {
            _logger.LogDebug("All {Count} requested movies found in cache", indexList.Count);
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

    public async Task<int> GetTotalMovieCountAsync()
    {
        _logger.LogDebug("Fetching total movie count from database");
        var count = (int)await _moviesCollection.CountDocumentsAsync(_ => true);
        _logger.LogInformation("Total movie count: {Count}", count);
        return count;
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

        _logger.LogDebug("Fetching {Count} movies by IDs", candidates.Count);

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
            _logger.LogDebug("Cache miss for {Count} movie IDs. Querying database.", missing.Count);

            var missingSet = new HashSet<int>(missing);
            var csfdFilter = Builders<MovieDocument>.Filter.In(d => d.CsfdId, missing.Select(id => (int?)id));
            var tmdbFilter = Builders<MovieDocument>.Filter.In(d => d.TmdbId, missing.Select(id => (int?)id));
            var filter = Builders<MovieDocument>.Filter.Or(csfdFilter, tmdbFilter);

            var documents = await _moviesCollection.Find(filter).ToListAsync();

            _logger.LogDebug("Found {Count} documents for {RequestedCount} missing IDs", documents.Count, missing.Count);

            foreach (var document in documents)
            {
                if (document.CsfdId.HasValue && missingSet.Remove(document.CsfdId.Value))
                {
                    var movie = _movieMapper.Map(document);
                    result[document.CsfdId.Value] = movie;
                    CacheMovieById(document.CsfdId.Value, movie);
                }
            }

            if (missingSet.Count > 0)
            {
                _logger.LogWarning("Could not find {Count} movies in database: {MissingIds}",
                    missingSet.Count, string.Join(", ", missingSet));
            }
        }
        else
        {
            _logger.LogDebug("All {Count} requested movies found in cache", candidates.Count);
        }

        return result;
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

    private static string GetMovieCacheKey(int index) => $"{MOVIE_INDEX_CACHE_KEY_PREFIX}{index}";
    private static string GetMovieByIdCacheKey(int id) => $"{MOVIE_ID_CACHE_KEY_PREFIX}{id}";
}
