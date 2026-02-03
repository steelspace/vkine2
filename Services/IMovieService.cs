using System.Collections.Generic;
using vkine.Models;

namespace vkine.Services;

public interface IMovieService
{
    Task<List<Movie>> GetMovies(int startIndex, int count);
    Task<int> GetTotalMovieCount();
    Task<List<Schedule>> GetTodaysSchedules();
    Task<List<Schedule>> SearchTodaysSchedules(string query);
    Task<Dictionary<int, Movie>> GetMoviesByIdsAsync(IEnumerable<int> ids);
    void InvalidatePerformanceCache();
}
