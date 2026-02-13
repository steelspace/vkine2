using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using vkine.Models;

namespace vkine.Services;

public class ScheduleService(
    IMongoDatabase database,
    IMemoryCache memoryCache,
    ILogger<ScheduleService> logger) : IScheduleService
{
    private const string SCHEDULES_COLLECTION_NAME = "schedule";
    private const string VENUES_COLLECTION_NAME = "venues";
    private const string UPCOMING_MOVIE_IDS_CACHE_KEY = "upcoming-movie-ids";
    private const string VENUES_CACHE_KEY_PREFIX = "venue-";

    private static readonly TimeSpan UpcomingMovieIdsCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VenueCacheDuration = TimeSpan.FromHours(1);

    private readonly IMongoCollection<ScheduleDto> _schedulesCollection = database.GetCollection<ScheduleDto>(SCHEDULES_COLLECTION_NAME);
    private readonly IMongoCollection<VenueDto> _venuesCollection = database.GetCollection<VenueDto>(VENUES_COLLECTION_NAME);
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<ScheduleService> _logger = logger;

    public async Task<List<ScheduleDto>> GetUpcomingPerformancesForMovieAsync(int movieId)
    {
        try
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            var filter = Builders<ScheduleDto>.Filter.And(
                Builders<ScheduleDto>.Filter.Eq(s => s.MovieId, movieId),
                Builders<ScheduleDto>.Filter.Gte(s => s.Date, today)
            );

            var schedules = await _schedulesCollection
                .Find(filter)
                .SortBy(s => s.Date)
                .ToListAsync();

            // Filter out past showtimes, keeping schedule structure intact
            foreach (var schedule in schedules)
            {
                foreach (var performance in schedule.Performances)
                {
                    performance.Showtimes = performance.Showtimes
                        .Where(st => schedule.Date.ToDateTime(st.StartAt) >= now)
                        .OrderBy(st => st.StartAt)
                        .ToList();
                }

                schedule.Performances = schedule.Performances
                    .Where(p => p.Showtimes.Count > 0)
                    .ToList();
            }

            return schedules.Where(s => s.Performances.Count > 0).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching upcoming performances for movie {MovieId}", movieId);
            return [];
        }
    }

    public async Task<Dictionary<int, VenueDto>> GetVenuesByIdsAsync(IEnumerable<int> venueIds)
    {
        try
        {
            var ids = venueIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, VenueDto>();
            }

            var result = new Dictionary<int, VenueDto>();
            var missing = new List<int>();

            // Check cache first
            foreach (var id in ids)
            {
                if (_memoryCache.TryGetValue($"{VENUES_CACHE_KEY_PREFIX}{id}", out VenueDto? cached) && cached is not null)
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
                var filter = Builders<VenueDto>.Filter.In(v => v.VenueId, missing);
                var venues = await _venuesCollection.Find(filter).ToListAsync();

                foreach (var venue in venues)
                {
                    result[venue.VenueId] = venue;
                    _memoryCache.Set(
                        $"{VENUES_CACHE_KEY_PREFIX}{venue.VenueId}",
                        venue,
                        new MemoryCacheEntryOptions
                        {
                            SlidingExpiration = VenueCacheDuration,
                            Size = 1
                        });
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching venues by IDs");
            return new Dictionary<int, VenueDto>();
        }
    }

    public async Task<List<int>> GetMovieIdsWithUpcomingPerformancesAsync(int skip, int limit)
    {
        var orderedIds = await GetCachedUpcomingMovieIdsAsync();

        return orderedIds
            .Skip(skip)
            .Take(limit)
            .ToList();
    }

    public async Task<HashSet<int>> GetAllMovieIdsWithUpcomingPerformancesAsync()
    {
        var orderedIds = await GetCachedUpcomingMovieIdsAsync();
        return orderedIds.ToHashSet();
    }

    /// <summary>
    /// Core method: fetches all schedules from today, computes movie IDs ordered by earliest showtime,
    /// and caches the result for 5 minutes.
    /// </summary>
    private async Task<List<int>> GetCachedUpcomingMovieIdsAsync()
    {
        if (_memoryCache.TryGetValue(UPCOMING_MOVIE_IDS_CACHE_KEY, out List<int>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            var filter = Builders<ScheduleDto>.Filter.Gte(s => s.Date, today);
            var schedules = await _schedulesCollection
                .Find(filter)
                .ToListAsync();

            var movieShowtimes = schedules
                .SelectMany(schedule => schedule.Performances
                    .SelectMany(performance => performance.Showtimes
                        .Select(showtime => new
                        {
                            schedule.MovieId,
                            ShowtimeDateTime = schedule.Date.ToDateTime(showtime.StartAt)
                        })))
                .Where(item => item.ShowtimeDateTime >= now)
                .GroupBy(item => item.MovieId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Min(item => item.ShowtimeDateTime));

            var result = movieShowtimes
                .OrderBy(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();

            _logger.LogInformation("Cached {Count} upcoming movie IDs for {Duration}",
                result.Count, UpcomingMovieIdsCacheDuration);

            _memoryCache.Set(UPCOMING_MOVIE_IDS_CACHE_KEY, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = UpcomingMovieIdsCacheDuration,
                Size = 1
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching upcoming movie IDs");
            return [];
        }
    }

    public async Task<List<int>> GetMovieIdsInDateRangeAsync(DateOnly from, DateOnly to)
    {
        var cacheKey = $"movie-ids-range-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}";

        if (_memoryCache.TryGetValue(cacheKey, out List<int>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var now = DateTime.Now;

            var filter = Builders<ScheduleDto>.Filter.And(
                Builders<ScheduleDto>.Filter.Gte(s => s.Date, from),
                Builders<ScheduleDto>.Filter.Lte(s => s.Date, to)
            );

            var schedules = await _schedulesCollection
                .Find(filter)
                .ToListAsync();

            var movieShowtimes = schedules
                .SelectMany(schedule => schedule.Performances
                    .SelectMany(performance => performance.Showtimes
                        .Select(showtime => new
                        {
                            schedule.MovieId,
                            ShowtimeDateTime = schedule.Date.ToDateTime(showtime.StartAt)
                        })))
                .Where(item => item.ShowtimeDateTime >= now) // still exclude past showtimes
                .GroupBy(item => item.MovieId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Min(item => item.ShowtimeDateTime));

            var result = movieShowtimes
                .OrderBy(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();

            _logger.LogInformation("Found {Count} movie IDs in date range {From} – {To}",
                result.Count, from, to);

            _memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = UpcomingMovieIdsCacheDuration,
                Size = 1
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching movie IDs in date range {From} – {To}", from, to);
            return [];
        }
    }
}
