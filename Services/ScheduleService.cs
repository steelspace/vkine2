using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using vkine.Models;

namespace vkine.Services;

public class ScheduleService(
    IMongoDatabase database,
    IMemoryCache memoryCache,
    ILogger<ScheduleService> logger) : IScheduleService
{
    private const string UpcomingMovieIdsCacheKey = "upcoming-movie-ids";
    private const string VenuesCacheKeyPrefix = "venue-";

    private static readonly TimeSpan UpcomingMovieIdsCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VenueCacheDuration = TimeSpan.FromHours(1);

    private readonly IMongoCollection<ScheduleDto> _schedulesCollection = database.GetCollection<ScheduleDto>("schedule");
    private readonly IMongoCollection<VenueDto> _venuesCollection = database.GetCollection<VenueDto>("venues");

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

            return schedules
                .Select(schedule => new ScheduleDto
                {
                    Id = schedule.Id,
                    Date = schedule.Date,
                    MovieId = schedule.MovieId,
                    MovieTitle = schedule.MovieTitle,
                    StoredAt = schedule.StoredAt,
                    Performances = schedule.Performances
                        .Select(p => new PerformanceDto
                        {
                            VenueId = p.VenueId,
                            Showtimes = p.Showtimes
                                .Where(st => schedule.Date.ToDateTime(st.StartAt) >= now)
                                .OrderBy(st => st.StartAt)
                                .ToList()
                        })
                        .Where(p => p.Showtimes.Count > 0)
                        .ToList()
                })
                .Where(s => s.Performances.Count > 0)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching upcoming performances for movie {MovieId}", movieId);
            return [];
        }
    }

    public async Task<Dictionary<int, VenueDto>> GetVenuesByIdsAsync(IEnumerable<int> venueIds)
    {
        try
        {
            var ids = venueIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, VenueDto>();

            var result = new Dictionary<int, VenueDto>();
            var missing = new List<int>();

            foreach (var id in ids)
            {
                if (memoryCache.TryGetValue($"{VenuesCacheKeyPrefix}{id}", out VenueDto? cached) && cached is not null)
                    result[id] = cached;
                else
                    missing.Add(id);
            }

            if (missing.Count > 0)
            {
                var filter = Builders<VenueDto>.Filter.In(v => v.VenueId, missing);
                var venues = await _venuesCollection.Find(filter).ToListAsync();

                foreach (var venue in venues)
                {
                    result[venue.VenueId] = venue;
                    memoryCache.Set($"{VenuesCacheKeyPrefix}{venue.VenueId}", venue,
                        new MemoryCacheEntryOptions { SlidingExpiration = VenueCacheDuration, Size = 1 });
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching venues by IDs");
            return new Dictionary<int, VenueDto>();
        }
    }

    public async Task<List<int>> GetMovieIdsWithUpcomingPerformancesAsync(int skip, int limit, TimeOnly? timeFrom = null)
    {
        var cacheKey = BuildCacheKey(UpcomingMovieIdsCacheKey, timeFrom);
        var orderedIds = await GetOrCreateCachedAsync(cacheKey, () => FetchMovieIdsAsync(
            Builders<ScheduleDto>.Filter.Gte(s => s.Date, DateOnly.FromDateTime(DateTime.Now)), timeFrom));

        return orderedIds.Skip(skip).Take(limit).ToList();
    }

    public async Task<HashSet<int>> GetAllMovieIdsWithUpcomingPerformancesAsync()
    {
        var cacheKey = BuildCacheKey(UpcomingMovieIdsCacheKey);
        var orderedIds = await GetOrCreateCachedAsync(cacheKey, () => FetchMovieIdsAsync(
            Builders<ScheduleDto>.Filter.Gte(s => s.Date, DateOnly.FromDateTime(DateTime.Now))));

        return orderedIds.ToHashSet();
    }

    public async Task<List<int>> GetMovieIdsInDateRangeAsync(DateOnly from, DateOnly to, TimeOnly? timeFrom = null)
    {
        var cacheKey = BuildCacheKey($"movie-ids-range-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}", timeFrom);

        return await GetOrCreateCachedAsync(cacheKey, () => FetchMovieIdsAsync(
            Builders<ScheduleDto>.Filter.And(
                Builders<ScheduleDto>.Filter.Gte(s => s.Date, from),
                Builders<ScheduleDto>.Filter.Lte(s => s.Date, to)),
            timeFrom));
    }

    /// <summary>
    /// Fetches schedules matching <paramref name="filter"/>, computes movie IDs ordered by earliest showtime.
    /// </summary>
    private async Task<List<int>> FetchMovieIdsAsync(FilterDefinition<ScheduleDto> filter, TimeOnly? timeFrom = null)
    {
        var now = DateTime.Now;

        var schedules = await _schedulesCollection
            .Find(filter)
            .ToListAsync();

        return schedules
            .SelectMany(s => s.Performances
                .SelectMany(p => p.Showtimes
                    .Where(st => !timeFrom.HasValue || st.StartAt >= timeFrom.Value)
                    .Select(st => (s.MovieId, ShowtimeAt: s.Date.ToDateTime(st.StartAt)))))
            .Where(x => x.ShowtimeAt >= now)
            .GroupBy(x => x.MovieId)
            .Select(g => (MovieId: g.Key, Earliest: g.Min(x => x.ShowtimeAt)))
            .OrderBy(x => x.Earliest)
            .Select(x => x.MovieId)
            .ToList();
    }

    private async Task<List<int>> GetOrCreateCachedAsync(string cacheKey, Func<Task<List<int>>> factory)
    {
        if (memoryCache.TryGetValue(cacheKey, out List<int>? cached) && cached is not null)
            return cached;

        try
        {
            var result = await factory();

            logger.LogInformation("Cached {Count} movie IDs for key '{CacheKey}' ({Duration})",
                result.Count, cacheKey, UpcomingMovieIdsCacheDuration);

            memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = UpcomingMovieIdsCacheDuration,
                Size = 1
            });

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching movie IDs for key '{CacheKey}'", cacheKey);
            return [];
        }
    }

    private static string BuildCacheKey(string prefix, TimeOnly? timeFrom = null) =>
        timeFrom.HasValue ? $"{prefix}-from-{timeFrom.Value:HHmm}" : prefix;
}
