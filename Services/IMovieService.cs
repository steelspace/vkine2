using vkine.Models;

namespace vkine.Services;

/// <summary>
/// Service interface for movie-related operations.
/// </summary>
public interface IMovieService
{
    /// <summary>
    /// Retrieves a paginated list of movies.
    /// </summary>
    /// <param name="startIndex">The starting index (0-based).</param>
    /// <param name="count">The number of movies to retrieve.</param>
    /// <returns>List of movies.</returns>
    Task<List<Movie>> GetMoviesAsync(int startIndex, int count);

    /// <summary>
    /// Gets the total count of movies in the database.
    /// </summary>
    /// <returns>Total movie count.</returns>
    Task<int> GetTotalMovieCountAsync();

    /// <summary>
    /// Retrieves movies by their IDs (CSFD or TMDB).
    /// </summary>
    /// <param name="ids">Collection of movie IDs to fetch.</param>
    /// <returns>Dictionary mapping IDs to Movie objects.</returns>
    Task<Dictionary<int, Movie>> GetMoviesByIdsAsync(IEnumerable<int> ids);
}
