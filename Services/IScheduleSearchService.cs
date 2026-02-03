using vkine.Models;

namespace vkine.Services;

public interface IScheduleSearchService
{
    List<Schedule> FilterByQuery(IEnumerable<Schedule> schedules, string query);
}
