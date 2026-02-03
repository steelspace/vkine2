using MongoDB.Driver;
using vkine.Models;

namespace vkine.Services.Caching;

/// <summary>
/// Provides cached access to a single day's schedules and their attached venues.
/// </summary>
public sealed class PerformanceCache
{
    private readonly IMongoCollection<Schedule> _schedulesCollection;
    private readonly IMongoCollection<Venue> _venuesCollection;
    private readonly object _sync = new();
    private List<Schedule>? _cachedSchedules;
    private DateOnly _cachedDay;
    private Task<List<Schedule>>? _inFlight;
    private DateOnly _inFlightDay;

    public PerformanceCache(IMongoCollection<Schedule> schedulesCollection, IMongoCollection<Venue> venuesCollection)
    {
        _schedulesCollection = schedulesCollection;
        _venuesCollection = venuesCollection;
    }

    public async Task<List<Schedule>> GetAsync(DateOnly day)
    {
        Task<List<Schedule>>? pending;

        lock (_sync)
        {
            if (_cachedSchedules != null && _cachedDay == day)
            {
                return new List<Schedule>(_cachedSchedules);
            }

            if (_inFlight != null && _inFlightDay == day)
            {
                pending = _inFlight;
            }
            else
            {
                pending = LoadAsync(day);
                _inFlight = pending;
                _inFlightDay = day;
            }
        }

        return await pending;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _cachedSchedules = null;
            _cachedDay = default;
            _inFlight = null;
            _inFlightDay = default;
        }
        }

    private async Task<List<Schedule>> LoadAsync(DateOnly day)
    {
        try
        {
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
                foreach (var performance in schedule.Performances)
                {
                    performance.Venue = venueMap.TryGetValue(performance.VenueId, out var venue) ? venue : null;
                }
            }

            lock (_sync)
            {
                _cachedSchedules = schedules;
                _cachedDay = day;
                return new List<Schedule>(_cachedSchedules);
            }
        }
        finally
        {
            lock (_sync)
            {
                if (_inFlightDay == day)
                {
                    _inFlight = null;
                    _inFlightDay = default;
                }
            }
        }
    }
}
