using System.Text.Json;
using vkine.Models;

namespace vkine.Services;

public class MovieService : IMovieService
{
    private readonly IWebHostEnvironment environment;

    public MovieService(IWebHostEnvironment environment)
    {
        this.environment = environment;
    }

    private List<Movie>? cachedMovies;

    private async Task EnsureMoviesLoaded()
    {
        if (cachedMovies == null)
        {
            var filePath = Path.Combine(environment.ContentRootPath, "Data", "movies.json");
            var jsonData = await File.ReadAllTextAsync(filePath);
            cachedMovies = JsonSerializer.Deserialize<List<Movie>>(jsonData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<Movie>();
        }
    }

    public async Task<List<Movie>> GetMovies(int startIndex, int count)
    {
        await EnsureMoviesLoaded();
        return cachedMovies!.Skip(startIndex).Take(count).ToList();
    }

    public async Task<int> GetTotalMovieCount()
    {
        await EnsureMoviesLoaded();
        return cachedMovies!.Count;
    }
}
