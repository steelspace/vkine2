using vkine.Models;

namespace vkine.Services;

public interface IMovieService
{
    Task<List<Movie>> GetMoviesAsync();
}
