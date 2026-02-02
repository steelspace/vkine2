using MongoDB.Driver;
using vkine.Models;

namespace vkine.Services;

public class MovieService : IMovieService
{
    private readonly IMongoCollection<MovieDocument> _moviesCollection;

    public MovieService(IMongoDatabase database)
    {
        _moviesCollection = database.GetCollection<MovieDocument>("movies1");
    }

    public async Task<List<Movie>> GetMovies(int startIndex, int count)
    {
        var documents = await _moviesCollection.Find(_ => true)
            .Skip(startIndex)
            .Limit(count)
            .ToListAsync();

        return documents.Select(d => new Movie
        {
            Id = int.TryParse(d.Id, out var id) ? id : 0,
            Title = !string.IsNullOrEmpty(d.Csfd?.CzechName) ? d.Csfd.CzechName : d.Csfd?.OriginalName ?? string.Empty,
            Synopsis = d.Csfd?.Plot ?? string.Empty,
            CoverUrl = d.Csfd?.PosterUrl ?? string.Empty
        }).ToList();
    }

    public async Task<int> GetTotalMovieCount()
    {
        return (int)await _moviesCollection.CountDocumentsAsync(_ => true);
    }
}
