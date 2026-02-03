using MongoDB.Driver;
using vkine.Models;
using vkine.Services.Caching;

namespace vkine.Services;

public class MovieService : IMovieService
{
    private const int MovieCacheCapacity = 256;

    private readonly IMongoCollection<MovieDocument> _moviesCollection;
    private readonly IMongoCollection<Schedule> _schedulesCollection;
    private readonly IMongoCollection<Venue> _venuesCollection;
    private readonly object _performanceCacheLock = new();
    private readonly MovieCache _movieCache;
    private List<Schedule>? _cachedSchedules;
    private DateOnly _cachedSchedulesDate;

    public MovieService(IMongoDatabase database)
    {
        _moviesCollection = database.GetCollection<MovieDocument>("movies");
        _schedulesCollection = database.GetCollection<Schedule>("schedule");
        _venuesCollection = database.GetCollection<Venue>("venues");
        _movieCache = new MovieCache(MovieCacheCapacity);
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
        var schedules = await EnsurePerformanceCacheAsync(today);
        return schedules.ToList();
    }

    public async Task<List<Schedule>> SearchTodaysSchedules(string query)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var schedules = await EnsurePerformanceCacheAsync(today);

        if (string.IsNullOrWhiteSpace(query))
        {
            return schedules.ToList();
        }

        var trimmed = query.Trim();

        return schedules
            .Where(schedule => MatchesQuery(schedule, trimmed))
            .ToList();
    }

    public void InvalidatePerformanceCache()
    {
        lock (_performanceCacheLock)
        {
            _cachedSchedules = null;
            _cachedSchedulesDate = default;
        }
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

    private async Task<List<Schedule>> EnsurePerformanceCacheAsync(DateOnly day)
    {
        var needsReload = false;

        lock (_performanceCacheLock)
        {
            if (_cachedSchedules != null && _cachedSchedulesDate == day)
            {
                return _cachedSchedules;
            }

            needsReload = true;
        }

        if (needsReload)
        {
            InvalidatePerformanceCache();
        }

        var start = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);

        var filter = Builders<Schedule>.Filter.And(
            Builders<Schedule>.Filter.Gte(s => s.Date, start),
            Builders<Schedule>.Filter.Lt(s => s.Date, end)
        );

        var schedules = await _schedulesCollection.Find(filter).ToListAsync();

        var venues = await _venuesCollection.Find(_ => true).ToListAsync();
        var venueMap = venues.ToDictionary(v => v.VenueId);

        foreach (var schedule in schedules)
        {
            foreach (var perf in schedule.Performances)
            {
                perf.Venue = venueMap.TryGetValue(perf.VenueId, out var venue) ? venue : null;
            }
        }

        lock (_performanceCacheLock)
        {
            _cachedSchedules = schedules;
            _cachedSchedulesDate = day;
            return _cachedSchedules;
        }
    }

    private static bool MatchesQuery(Schedule schedule, string query)
    {
        if (schedule.MovieTitle.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var performance in schedule.Performances)
        {
            if (performance.Venue != null)
            {
                if (!string.IsNullOrEmpty(performance.Venue.Name) &&
                    performance.Venue.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(performance.Venue.Address) &&
                    performance.Venue.Address.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(performance.Venue.City) &&
                    performance.Venue.City.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (var showtime in performance.Showtimes)
            {
                if (showtime.Badges.Any(b =>
                        (!string.IsNullOrEmpty(b.Description) && b.Description.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(b.Code) && b.Code.Contains(query, StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(showtime.TicketUrl) &&
                    showtime.TicketUrl.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
