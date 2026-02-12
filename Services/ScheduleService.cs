using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using vkine.Models;

namespace vkine.Services;

public class ScheduleService(
    IMongoDatabase database,
    ILogger<ScheduleService> logger) : IScheduleService
{
    private const string SCHEDULES_COLLECTION_NAME = "schedule";
    private const string VENUES_COLLECTION_NAME = "venues";

    private readonly IMongoCollection<ScheduleDto> _schedulesCollection = database.GetCollection<ScheduleDto>(SCHEDULES_COLLECTION_NAME);
    private readonly IMongoCollection<VenueDto> _venuesCollection = database.GetCollection<VenueDto>(VENUES_COLLECTION_NAME);
    private readonly ILogger<ScheduleService> _logger = logger;

    public async Task<List<ScheduleDto>> GetUpcomingPerformancesForMovieAsync(int movieId)
    {
        try
        {
            _logger.LogInformation("Fetching schedules for movie {MovieId}", movieId);

            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var currentTime = TimeOnly.FromDateTime(now);

            _logger.LogInformation("Current local time: {Now}, Today: {Today}, Current time: {CurrentTime}", 
                now.ToString("yyyy-MM-dd HH:mm:ss"), today, currentTime);

            // First, check if there are any schedules for this movie at all
            var anyScheduleFilter = Builders<ScheduleDto>.Filter.Eq(s => s.MovieId, movieId);
            var totalCount = await _schedulesCollection.CountDocumentsAsync(anyScheduleFilter);
            _logger.LogInformation("Total schedules found for movie {MovieId}: {Count}", movieId, totalCount);

            // Now filter by date
            var filter = Builders<ScheduleDto>.Filter.And(
                Builders<ScheduleDto>.Filter.Eq(s => s.MovieId, movieId),
                Builders<ScheduleDto>.Filter.Gte(s => s.Date, today)
            );

            var schedules = await _schedulesCollection
                .Find(filter)
                .SortBy(s => s.Date)
                .ToListAsync();

            _logger.LogInformation("Schedules found after date filter: {Count}", schedules.Count);

            if (schedules.Count > 0)
            {
                _logger.LogInformation("First schedule date: {Date}, Performances: {Count}",
                    schedules[0].Date, schedules[0].Performances.Count);
            }

            var initialScheduleCount = schedules.Count;
            var initialShowtimeCount = schedules.Sum(s => s.Performances.Sum(p => p.Showtimes.Count));

            // Log sample showtime data for debugging
            if (schedules.Count > 0 && schedules[0].Performances.Count > 0 && schedules[0].Performances[0].Showtimes.Count > 0)
            {
                var firstShowtime = schedules[0].Performances[0].Showtimes[0];
                _logger.LogInformation("Sample: Schedule.Date={ScheduleDate}, Showtime.StartAt={StartAt}", 
                    schedules[0].Date, firstShowtime.StartAt);
            }

            // Filter out past showtimes, keeping schedule structure intact
            foreach (var schedule in schedules)
            {
                foreach (var performance in schedule.Performances)
                {
                    var before = performance.Showtimes.Count;
                    
                    // Only keep future showtimes - combine date with time for comparison
                    performance.Showtimes = performance.Showtimes
                        .Where(st =>
                        {
                            var showtimeDateTime = schedule.Date.ToDateTime(st.StartAt);
                            var isFuture = showtimeDateTime >= now;
                            if (!isFuture)
                            {
                                _logger.LogDebug("Filtering out past showtime: {ShowtimeDateTime} < {Now}",
                                    showtimeDateTime.ToString("yyyy-MM-dd HH:mm:ss"), now.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                            return isFuture;
                        })
                        .OrderBy(st => st.StartAt)
                        .ToList();
                    
                    if (before > performance.Showtimes.Count)
                    {
                        _logger.LogInformation("Filtered {Count} past showtimes from venue {VenueId}",
                            before - performance.Showtimes.Count, performance.VenueId);
                    }
                }

                // Remove performances with no upcoming showtimes
                schedule.Performances = schedule.Performances
                    .Where(p => p.Showtimes.Count > 0)
                    .ToList();
            }

            // Remove schedules with no performances
            schedules = schedules.Where(s => s.Performances.Count > 0).ToList();

            var finalShowtimeCount = schedules.Sum(s => s.Performances.Sum(p => p.Showtimes.Count));

            _logger.LogInformation("After filtering: {InitialSchedules} -> {FinalSchedules} schedules, {InitialShowtimes} -> {FinalShowtimes} showtimes",
                initialScheduleCount, schedules.Count, initialShowtimeCount, finalShowtimeCount);

            return schedules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching upcoming performances for movie {MovieId}", movieId);
            return new List<ScheduleDto>();
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

            var filter = Builders<VenueDto>.Filter.In(v => v.VenueId, ids);
            var venues = await _venuesCollection.Find(filter).ToListAsync();

            return venues.ToDictionary(v => v.VenueId, v => v);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching venues by IDs");
            return new Dictionary<int, VenueDto>();
        }
    }

    public async Task<List<int>> GetMovieIdsWithUpcomingPerformancesAsync(int skip, int limit)
    {
        try
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var currentTime = TimeOnly.FromDateTime(now);

            _logger.LogInformation("Fetching movie IDs with performances >= {Today} {Time}", today, currentTime);

            // Fetch all schedules from today onwards
            var filter = Builders<ScheduleDto>.Filter.Gte(s => s.Date, today);
            var schedules = await _schedulesCollection
                .Find(filter)
                .ToListAsync();

            _logger.LogInformation("Found {Count} schedules from today onwards", schedules.Count);

            // Process in memory: find earliest upcoming showtime for each movie
            var movieShowtimes = new Dictionary<int, DateTime>();

            foreach (var schedule in schedules)
            {
                foreach (var performance in schedule.Performances)
                {
                    foreach (var showtime in performance.Showtimes)
                    {
                        var showtimeDateTime = schedule.Date.ToDateTime(showtime.StartAt);
                        
                        // Only consider future showtimes
                        if (showtimeDateTime >= now)
                        {
                            if (!movieShowtimes.ContainsKey(schedule.MovieId) || 
                                showtimeDateTime < movieShowtimes[schedule.MovieId])
                            {
                                movieShowtimes[schedule.MovieId] = showtimeDateTime;
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("Found {Count} unique movies with upcoming showtimes", movieShowtimes.Count);

            // Sort by earliest showtime, apply paging
            var result = movieShowtimes
                .OrderBy(kvp => kvp.Value)
                .Skip(skip)
                .Take(limit)
                .Select(kvp => kvp.Key)
                .ToList();

            _logger.LogInformation("Returning {Count} movie IDs after skip={Skip} limit={Limit}", result.Count, skip, limit);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching movie IDs with upcoming performances");
            return new List<int>();
        }
    }
}
