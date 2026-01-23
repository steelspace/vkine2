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

    public async Task<List<Movie>> GetMoviesAsync()
    {
        var filePath = Path.Combine(environment.ContentRootPath, "Data", "movies.json");
        var jsonData = await File.ReadAllTextAsync(filePath);
        var movies = JsonSerializer.Deserialize<List<Movie>>(jsonData, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return movies ?? new List<Movie>();
    }
}
