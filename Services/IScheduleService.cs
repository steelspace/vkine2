using vkine.Models;

namespace vkine.Services;

public interface IScheduleService
{
    Task<List<ScheduleDto>> GetUpcomingPerformancesForMovieAsync(int movieId);
    Task<Dictionary<int, VenueDto>> GetVenuesByIdsAsync(IEnumerable<int> venueIds);

    // Returns a page of distinct movie IDs that have at least one upcoming showtime (today or later),
    // ordered by earliest upcoming showtime. Paging is controlled by skip/limit.
    // When timeFrom is specified, only showtimes at or after that time of day are considered.
    Task<List<int>> GetMovieIdsWithUpcomingPerformancesAsync(int skip, int limit, TimeOnly? timeFrom = null);

    // Returns a set of all movie IDs that have at least one upcoming showtime (today or later).
    Task<HashSet<int>> GetAllMovieIdsWithUpcomingPerformancesAsync();

    // Returns movie IDs with performances in the given date range, ordered by earliest showtime in range.
    // When timeFrom is specified, only showtimes at or after that time of day are considered.
    Task<List<int>> GetMovieIdsInDateRangeAsync(DateOnly from, DateOnly to, TimeOnly? timeFrom = null);
}