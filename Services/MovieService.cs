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

    public async Task<List<Schedule>> SearchTodaysSchedules(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetTodaysSchedules();
        }

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var dateFilter = Builders<Schedule>.Filter.And(
            Builders<Schedule>.Filter.Gte(s => s.Date, today),
            Builders<Schedule>.Filter.Lt(s => s.Date, tomorrow)
        );

        // case-insensitive regex
        var regex = new BsonRegularExpression(query, "i");

        // search movie documents in multiple likely fields (title, description, localized title, common crew paths)
        var movieFilter = Builders<MovieDocument>.Filter.Or(
            Builders<MovieDocument>.Filter.Regex("title", regex),
            Builders<MovieDocument>.Filter.Regex("description", regex),
            Builders<MovieDocument>.Filter.Regex("localized_titles.Original", regex),
            Builders<MovieDocument>.Filter.Regex("crew.name", regex),
            Builders<MovieDocument>.Filter.Regex("credits.cast.name", regex)
        );

        var matchedMovies = await _moviesCollection.Find(movieFilter).ToListAsync();

        var idSet = new HashSet<int>();
        foreach (var m in matchedMovies)
        {
            if (m.CsfdId.HasValue) idSet.Add(m.CsfdId.Value);
            if (m.TmdbId.HasValue) idSet.Add(m.TmdbId.Value);
        }

        // build schedule filter: date + (movie id in matched ids OR movie_title matches regex)
        var scheduleFilters = new List<FilterDefinition<Schedule>> { dateFilter };

        var orFilters = new List<FilterDefinition<Schedule>>();
        if (idSet.Any())
        {
            orFilters.Add(Builders<Schedule>.Filter.In("movie_id", idSet));
        }
        orFilters.Add(Builders<Schedule>.Filter.Regex("movie_title", regex));

        scheduleFilters.Add(Builders<Schedule>.Filter.Or(orFilters));

        var finalFilter = Builders<Schedule>.Filter.And(scheduleFilters);

        var schedules = await _schedulesCollection.Find(finalFilter).ToListAsync();

        // attach venues (same as GetTodaysSchedules)
        var venues = await _venuesCollection.Find(_ => true).ToListAsync();
        var venueMap = venues.ToDictionary(v => v.VenueId);

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
