using Microsoft.AspNetCore.Components;
using vkine.Models;
using vkine.Services;

namespace vkine.Components.Pages;

public partial class Movies : ComponentBase
{
    [Inject]
    private IMovieService MovieService { get; set; } = default!;

    [Inject]
    private IScheduleService ScheduleService { get; set; } = default!;

    private List<Movie> movies = new();
    private bool isLoading = true;
    private bool isLoadingMore = false;
    private const int PageSize = 20;
    private bool isModalOpen = false;
    private Movie? selectedMovie = null;

    // Paging state for schedule-based movie IDs
    private int _skip = 0;
    private bool _hasMore = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadMovies();
    }

    private async Task LoadMovies()
    {
        try
        {
            isLoading = true;
            movies.Clear();
            _skip = 0;
            _hasMore = true;

            await LoadMoreMovies();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading movies: {ex.Message}");
            movies = new List<Movie>();
            _hasMore = false;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadMoreMovies()
    {
        if (!_hasMore) return;

        try
        {
            isLoadingMore = true;

            var ids = await ScheduleService.GetMovieIdsWithUpcomingPerformancesAsync(_skip, PageSize);
            if (ids.Count == 0)
            {
                _hasMore = false;
                return;
            }

            _skip += ids.Count;
            if (ids.Count < PageSize) _hasMore = false;

            var moviesById = await MovieService.GetMoviesByIdsAsync(ids);
            var pageMovies = ids.Where(id => moviesById.ContainsKey(id)).Select(id => moviesById[id]).ToList();

            // Append found movies
            movies.AddRange(pageMovies);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading more movies: {ex.Message}");
        }
        finally
        {
            isLoadingMore = false;
        }
    }

    private void OpenModal(Movie movie)
    {
        selectedMovie = movie;
        isModalOpen = true;
    }

    private void CloseModal()
    {
        isModalOpen = false;
        selectedMovie = null;
    }
}
