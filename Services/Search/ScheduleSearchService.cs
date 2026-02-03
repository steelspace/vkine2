using vkine.Models;

namespace vkine.Services.Search;

public sealed class ScheduleSearchService : IScheduleSearchService
{
    public List<Schedule> FilterByQuery(IEnumerable<Schedule> schedules, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return schedules.ToList();
        }

        var trimmed = query.Trim();
        return schedules.Where(schedule => Matches(schedule, trimmed)).ToList();
    }

    private static bool Matches(Schedule schedule, string query)
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
