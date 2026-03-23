using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using vkine.Api;
using vkine.Models;
using vkine.Services;

namespace vkine.Tests;

public class ApiEndpointsTests
{
    private readonly Mock<IMovieService> _movies = new();
    private readonly Mock<IScheduleService> _schedules = new();
    private readonly Mock<IPremiereService> _premieres = new();

    // ── GetMovies ────────────────────────────────────────────

    [Fact]
    public async Task GetMovies_ReturnsMoviesOrderedBySchedule()
    {
        _schedules.Setup(s => s.GetMovieIdsWithUpcomingPerformancesAsync(0, 2, null))
            .ReturnsAsync([2, 1]);
        _movies.Setup(m => m.GetMoviesByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, Movie>
            {
                [1] = new() { Id = 1 },
                [2] = new() { Id = 2 }
            });

        var result = await ApiEndpoints.GetMoviesAsync(_movies.Object, _schedules.Object, 0, 2);

        Assert.Equal([2, 1], result.Value!.Select(m => m.Id));
    }

    [Fact]
    public async Task GetMovies_SkipsIdsMissingFromMovieDictionary()
    {
        _schedules.Setup(s => s.GetMovieIdsWithUpcomingPerformancesAsync(0, 10, null))
            .ReturnsAsync([1, 2, 3]);
        _movies.Setup(m => m.GetMoviesByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, Movie>
            {
                [1] = new() { Id = 1 },
                [3] = new() { Id = 3 }
            });

        var result = await ApiEndpoints.GetMoviesAsync(_movies.Object, _schedules.Object, 0, 10);

        Assert.Equal([1, 3], result.Value!.Select(m => m.Id));
    }

    [Fact]
    public async Task GetMovies_EmptySchedule_ReturnsEmptyList()
    {
        _schedules.Setup(s => s.GetMovieIdsWithUpcomingPerformancesAsync(0, 20, null))
            .ReturnsAsync([]);
        _movies.Setup(m => m.GetMoviesByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new Dictionary<int, Movie>());

        var result = await ApiEndpoints.GetMoviesAsync(_movies.Object, _schedules.Object, 0, 20);

        Assert.Empty(result.Value!);
    }

    // ── SearchMovies ─────────────────────────────────────────

    [Fact]
    public async Task SearchMovies_EmptyQuery_ReturnsBadRequest()
    {
        var result = await ApiEndpoints.SearchMoviesAsync(_movies.Object, "");

        Assert.IsType<BadRequest<string>>(result.Result);
    }

    [Fact]
    public async Task SearchMovies_WhitespaceQuery_ReturnsBadRequest()
    {
        var result = await ApiEndpoints.SearchMoviesAsync(_movies.Object, "   ");

        Assert.IsType<BadRequest<string>>(result.Result);
    }

    [Fact]
    public async Task SearchMovies_NullQuery_ReturnsBadRequest()
    {
        var result = await ApiEndpoints.SearchMoviesAsync(_movies.Object, null);

        Assert.IsType<BadRequest<string>>(result.Result);
    }

    [Fact]
    public async Task SearchMovies_ValidQuery_ReturnsMatches()
    {
        var movies = new List<Movie> { new() { Id = 42, Title = "Batman" } };
        _movies.Setup(m => m.SearchMoviesAsync("batman", 50)).ReturnsAsync(movies);

        var result = await ApiEndpoints.SearchMoviesAsync(_movies.Object, "batman");

        var ok = Assert.IsType<Ok<List<Movie>>>(result.Result);
        Assert.Single(ok.Value!);
        Assert.Equal(42, ok.Value[0].Id);
    }

    [Fact]
    public async Task SearchMovies_NoMatches_ReturnsEmptyList()
    {
        _movies.Setup(m => m.SearchMoviesAsync("xyz", 50)).ReturnsAsync([]);

        var result = await ApiEndpoints.SearchMoviesAsync(_movies.Object, "xyz");

        var ok = Assert.IsType<Ok<List<Movie>>>(result.Result);
        Assert.Empty(ok.Value!);
    }

    // ── GetShowtimes ─────────────────────────────────────────

    [Fact]
    public async Task GetShowtimes_ReturnsSchedulesForMovie()
    {
        var schedules = new List<ScheduleDto> { new() { MovieId = 5 } };
        _schedules.Setup(s => s.GetUpcomingPerformancesForMovieAsync(5)).ReturnsAsync(schedules);

        var result = await ApiEndpoints.GetShowtimesAsync(_schedules.Object, 5);

        Assert.Single(result.Value!);
        Assert.Equal(5, result.Value[0].MovieId);
    }

    [Fact]
    public async Task GetShowtimes_NoUpcoming_ReturnsEmptyList()
    {
        _schedules.Setup(s => s.GetUpcomingPerformancesForMovieAsync(99)).ReturnsAsync([]);

        var result = await ApiEndpoints.GetShowtimesAsync(_schedules.Object, 99);

        Assert.Empty(result.Value!);
    }

    // ── GetPremieres ─────────────────────────────────────────

    [Fact]
    public async Task GetPremieres_ReturnsList()
    {
        var premieres = new List<PremiereDocument> { new() { CsfdId = 99 } };
        _premieres.Setup(p => p.GetUpcomingPremieresAsync()).ReturnsAsync(premieres);

        var result = await ApiEndpoints.GetPremieresAsync(_premieres.Object);

        Assert.Single(result.Value!);
        Assert.Equal(99, result.Value[0].CsfdId);
    }

    [Fact]
    public async Task GetPremieres_Empty_ReturnsEmptyList()
    {
        _premieres.Setup(p => p.GetUpcomingPremieresAsync()).ReturnsAsync([]);

        var result = await ApiEndpoints.GetPremieresAsync(_premieres.Object);

        Assert.Empty(result.Value!);
    }
}
