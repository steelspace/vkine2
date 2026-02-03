using MongoDB.Driver;
using vkine.Models;
using vkine.Services.Caching;

namespace vkine.Services;

public class MovieService : IMovieService
{
    private const int MovieCacheCapacity = 256;

    private readonly IMongoCollection<MovieDocument> _moviesCollection;
    private readonly PerformanceCache _performanceCache;
    private readonly IScheduleSearchService _scheduleSearchService;
    private readonly MovieCache _movieCache;

    public MovieService(IMongoDatabase database, IScheduleSearchService scheduleSearchService)
    {
        _moviesCollection = database.GetCollection<MovieDocument>("movies");
        var schedulesCollection = database.GetCollection<Schedule>("schedule");
        var venuesCollection = database.GetCollection<Venue>("venues");
        _performanceCache = new PerformanceCache(schedulesCollection, venuesCollection);
        _movieCache = new MovieCache(MovieCacheCapacity);
        _scheduleSearchService = scheduleSearchService;
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

        var cacheHits = _movieCache.SnapshotForIndexes(indexList, out var missingIndexes);

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
                    .Select(d => new Movie
                    {
                        Id = d.CsfdId ?? d.TmdbId ?? 0,
                        Title = !string.IsNullOrEmpty(d.Title) ? d.Title : d.LocalizedTitles?.Original ?? string.Empty,
                        Synopsis = d.Description ?? string.Empty,
                        CoverUrl = d.PosterUrl ?? string.Empty
                    })
                    .ToList();

                for (var offset = 0; offset < mapped.Count; offset++)
                {
                    var index = rangeStart + offset;
                    var movie = mapped[offset];
                    cacheHits[index] = movie;
                    _movieCache.Store(index, movie);
                }
            }
        }

        var ordered = new List<Movie>(indexList.Count);

        foreach (var index in indexList)
        {
            if (cacheHits.TryGetValue(index, out var movie))
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

}
