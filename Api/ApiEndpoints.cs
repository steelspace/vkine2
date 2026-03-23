using Microsoft.AspNetCore.Http.HttpResults;
using vkine.Models;
using vkine.Services;

namespace vkine.Api;

public static class ApiEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/movies", GetMoviesAsync);
        api.MapGet("/movies/search", SearchMoviesAsync);
        api.MapGet("/movies/{csfdId:int}/showtimes", GetShowtimesAsync);
        api.MapGet("/premieres", GetPremieresAsync);
    }

    public static async Task<Ok<List<Movie>>> GetMoviesAsync(
        IMovieService movies, IScheduleService schedules, int skip = 0, int take = 20)
    {
        var ids = await schedules.GetMovieIdsWithUpcomingPerformancesAsync(skip, take);
        var byId = await movies.GetMoviesByIdsAsync(ids);
        var ordered = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        return TypedResults.Ok(ordered);
    }

    public static async Task<Results<Ok<List<Movie>>, BadRequest<string>>> SearchMoviesAsync(
        IMovieService movies, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return TypedResults.BadRequest("q is required");
        var results = await movies.SearchMoviesAsync(q);
        return TypedResults.Ok(results);
    }

    public static async Task<Ok<List<ScheduleDto>>> GetShowtimesAsync(
        IScheduleService schedules, int csfdId)
    {
        var showtimes = await schedules.GetUpcomingPerformancesForMovieAsync(csfdId);
        return TypedResults.Ok(showtimes);
    }

    public static async Task<Ok<List<PremiereDocument>>> GetPremieresAsync(
        IPremiereService premieres)
    {
        var results = await premieres.GetUpcomingPremieresAsync();
        return TypedResults.Ok(results);
    }
}
