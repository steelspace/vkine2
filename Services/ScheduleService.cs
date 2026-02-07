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

            var now = DateTime.UtcNow;
            var today = now.Date;

            _logger.LogInformation("Current UTC time: {Now}, Today date: {Today}", now, today);

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

            // Filter out past showtimes within today's schedules
            foreach (var schedule in schedules)
            {
                foreach (var performance in schedule.Performances)
                {
                    performance.Showtimes = performance.Showtimes
                        .Where(st => st.StartAt >= now)
                        .OrderBy(st => st.StartAt)
                        .ToList();
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
            var now = DateTime.UtcNow;

            // Use constants for nested fields to avoid scattering string literals
            const string showtimeField = "performances.showtimes.start_at";

            // Build aggregation stages using BsonDocument for unwind/group and Builders for match where convenient
            var unwindPerf = new BsonDocument("$unwind", "$performances");
            var unwindShow = new BsonDocument("$unwind", "$performances.showtimes");
            var match = new BsonDocument("$match", new BsonDocument(showtimeField, new BsonDocument("$gte", now)));
            var group = new BsonDocument("$group", new BsonDocument { { "_id", "$movie_id" }, { "firstShow", new BsonDocument("$min", "$performances.showtimes.start_at") } });
            var sort = new BsonDocument("$sort", new BsonDocument("firstShow", 1));
            var skipDoc = new BsonDocument("$skip", skip);
            var limitDoc = new BsonDocument("$limit", limit);

            var pipeline = new[] { unwindPerf, unwindShow, match, group, sort, skipDoc, limitDoc };
            var results = await _schedulesCollection.Aggregate<BsonDocument>(pipeline).ToListAsync();

            return results.Select(d => d["_id"].AsInt32).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching movie IDs with upcoming performances");
            return new List<int>();
        }
    }
}
