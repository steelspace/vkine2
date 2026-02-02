using vkine.Models;

namespace vkine.Services;

public interface IMovieService
{
    Task<List<Movie>> GetMovies(int startIndex, int count);
    Task<int> GetTotalMovieCount();
    Task<List<Schedule>> GetTodaysSchedules();
}
