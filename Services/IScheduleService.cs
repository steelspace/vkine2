using vkine.Models;

namespace vkine.Services;

public interface IScheduleService
{
    Task<List<ScheduleDto>> GetUpcomingPerformancesForMovieAsync(int movieId);
    Task<Dictionary<int, VenueDto>> GetVenuesByIdsAsync(IEnumerable<int> venueIds);
}
