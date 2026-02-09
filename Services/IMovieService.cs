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
    /// Search movies by free-text query across title, description, cast and crew.
    /// Queries are tokenized and require all tokens to appear in any of the searchable fields (AND semantics).
    /// This is tuned to behave like a Google-like quick search.
    /// </summary>
    /// <param name="query">The free text query.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    Task<List<Movie>> SearchMoviesAsync(string query, int limit = 50);

    /// <summary>
    /// Gets the total count of movies in the database.
    /// </summary>
    /// <returns>Total movie count.</returns>
    Task<int> GetTotalMovieCountAsync();

    /// <summary>
    /// Retrieves movies by their CSFD IDs.
    /// </summary>
    /// <param name="ids">Collection of CSFD IDs to fetch.</param>
    /// <returns>Dictionary mapping CSFD IDs to Movie objects.</returns>
    Task<Dictionary<int, Movie>> GetMoviesByIdsAsync(IEnumerable<int> ids);
}
