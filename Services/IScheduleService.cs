using vkine.Models;

namespace vkine.Services;

public interface IScheduleService
{
    Task<List<ScheduleDto>> GetUpcomingPerformancesForMovieAsync(int movieId);
    Task<Dictionary<int, VenueDto>> GetVenuesByIdsAsync(IEnumerable<int> venueIds);

    // Returns a page of distinct movie IDs that have at least one upcoming showtime (today or later),
    // ordered by earliest upcoming showtime. Paging is controlled by skip/limit.
    Task<List<int>> GetMovieIdsWithUpcomingPerformancesAsync(int skip, int limit);
}