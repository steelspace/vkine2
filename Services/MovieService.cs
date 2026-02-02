using MongoDB.Driver;
using MongoDB.Bson;
using vkine.Models;

namespace vkine.Services;

public class MovieService : IMovieService
{
    private readonly IMongoCollection<MovieDocument> _moviesCollection;
    private readonly IMongoCollection<Schedule> _schedulesCollection;
    private readonly IMongoCollection<Venue> _venuesCollection;

    public MovieService(IMongoDatabase database)
    {
        _moviesCollection = database.GetCollection<MovieDocument>("movies");
        _schedulesCollection = database.GetCollection<Schedule>("schedule");
        _venuesCollection = database.GetCollection<Venue>("venues");
    }

    public async Task<List<Movie>> GetMovies(int startIndex, int count)
    {
var documents = await _moviesCollection.Find(_ => true)
            .Skip(startIndex)
            .Limit(count)
            .ToListAsync();

        return documents.Select(d => new Movie
        {
            Id = d.CsfdId ?? d.TmdbId ?? 0,
            Title = !string.IsNullOrEmpty(d.Title) ? d.Title : d.LocalizedTitles?.Original ?? string.Empty,
            Synopsis = d.Description ?? string.Empty,
            CoverUrl = d.PosterUrl ?? string.Empty
        }).ToList();
    }

    public async Task<int> GetTotalMovieCount()
    {
        return (int)await _moviesCollection.CountDocumentsAsync(_ => true);
    }

    public async Task<List<Schedule>> GetTodaysSchedules()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var filter = Builders<Schedule>.Filter.And(
            Builders<Schedule>.Filter.Gte(s => s.Date, today),
            Builders<Schedule>.Filter.Lt(s => s.Date, tomorrow)
        );

        var schedules = await _schedulesCollection.Find(filter).ToListAsync();

        // Prefetch all venues and map by venue_id
        var venues = await _venuesCollection.Find(_ => true).ToListAsync();
        var venueMap = venues.ToDictionary(v => v.VenueId);

        // Attach venue references to performances
        foreach (var schedule in schedules)
        {
            foreach (var perf in schedule.Performances)
            {
                if (venueMap.TryGetValue(perf.VenueId, out var venue))
                {
                    perf.Venue = venue;
                }
                else
                {
                    perf.Venue = null;
                }
            }
        }

        return schedules;
    }
}
