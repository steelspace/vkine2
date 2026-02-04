using System.Collections.Generic;
using vkine.Models;

namespace vkine.Services;

public interface IMovieService
{
    Task<List<Movie>> GetMovies(int startIndex, int count);
    Task<int> GetTotalMovieCount();

    // New: only movies that have any future schedule in the database
    Task<List<Movie>> GetMoviesWithFutureSchedules(int startIndex, int count);
    Task<int> GetTotalMovieCountWithFutureSchedules();

    Task<List<Schedule>> GetTodaysSchedules();
    Task<List<Schedule>> SearchTodaysSchedules(string query);

    // New: schedules grouped by day for a specific movie (upcoming days)
    Task<Dictionary<DateOnly, List<Schedule>>> GetUpcomingSchedulesForMovie(int movieId, int days);

    Task<Dictionary<int, Movie>> GetMoviesByIdsAsync(IEnumerable<int> ids);
    void InvalidatePerformanceCache();
}
