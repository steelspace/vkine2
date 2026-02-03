using MongoDB.Driver;
using vkine.Models;

namespace vkine.Services;

public class MovieService : IMovieService
{
    private const int MovieCacheCapacity = 256;

    private readonly IMongoCollection<MovieDocument> _moviesCollection;
    private readonly IMongoCollection<Schedule> _schedulesCollection;
    private readonly IMongoCollection<Venue> _venuesCollection;
    private readonly object _movieCacheLock = new();
    private readonly object _performanceCacheLock = new();
    private readonly Dictionary<int, Movie> _movieCache = new();
    private readonly Queue<int> _movieOrder = new();
    private List<Schedule>? _cachedSchedules;
    private DateOnly _cachedSchedulesDate;

    public MovieService(IMongoDatabase database)
    {
        _moviesCollection = database.GetCollection<MovieDocument>("movies");
        _schedulesCollection = database.GetCollection<Schedule>("schedule");
        _venuesCollection = database.GetCollection<Venue>("venues");
    }

    public async Task<List<Movie>> GetMovies(int startIndex, int count)
    {
        if (count <= 0)
        {
            return new List<Movie>();
        }

        var indexList = Enumerable.Range(startIndex, count).ToList();

        while (true)
        {
            if (indexList.Count == 0)
            {
                return new List<Movie>();
            }

            List<(int start, int length)> missing;

            lock (_movieCacheLock)
            {
                missing = GetMissingRanges(indexList);
                if (missing.Count == 0)
                {
                    return indexList.Select(idx => _movieCache[idx]).ToList();
                }
            }

            var anyInserted = false;

            foreach (var (rangeStart, rangeLength) in missing)
            {
                var documents = await _moviesCollection.Find(_ => true)
                    .Skip(rangeStart)
                    .Limit(rangeLength)
                    .ToListAsync();

                if (documents.Count == 0)
                {
                    indexList.RemoveAll(idx => idx >= rangeStart && idx < rangeStart + rangeLength);
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

                lock (_movieCacheLock)
                {
                    for (var offset = 0; offset < mapped.Count; offset++)
                    {
                        AddMovieToCache(rangeStart + offset, mapped[offset]);
                    }
                }

                if (mapped.Count < rangeLength)
                {
                    var removalStart = rangeStart + mapped.Count;
                    var removalEnd = rangeStart + rangeLength;
                    indexList.RemoveAll(idx => idx >= removalStart && idx < removalEnd);
                }

                anyInserted = true;
            }

            if (!anyInserted)
            {
                break;
            }
        }

        lock (_movieCacheLock)
        {
            return indexList
                .Where(idx => _movieCache.TryGetValue(idx, out _))
                .Select(idx => _movieCache[idx])
                .ToList();
        }
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

    private void AddMovieToCache(int index, Movie movie)
    {
        if (MovieCacheCapacity <= 0)
        {
            return;
        }

        if (_movieCache.TryGetValue(index, out _))
        {
            _movieCache[index] = movie;
            return;
        }

        _movieCache[index] = movie;
        _movieOrder.Enqueue(index);

        while (_movieCache.Count > MovieCacheCapacity && _movieOrder.TryDequeue(out var toRemove))
        {
            if (_movieCache.Remove(toRemove))
            {
                break;
            }
        }
    }

    private List<(int start, int length)> GetMissingRanges(List<int> indexes)
    {
        var missing = new List<(int start, int length)>();

        if (indexes.Count == 0)
        {
            return missing;
        }

        var currentStart = -1;
        var previousIndex = -1;

        foreach (var index in indexes)
        {
            if (_movieCache.ContainsKey(index))
            {
                if (currentStart >= 0)
                {
                    missing.Add((currentStart, previousIndex - currentStart + 1));
                    currentStart = -1;
                }

                continue;
            }

            if (currentStart < 0)
            {
                currentStart = index;
            }

            previousIndex = index;
        }

        if (currentStart >= 0)
        {
            missing.Add((currentStart, indexes[^1] - currentStart + 1));
        }

        return missing;
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
