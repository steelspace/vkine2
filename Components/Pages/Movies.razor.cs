using Microsoft.AspNetCore.Components;
using vkine.Models;
using vkine.Services;

namespace vkine.Components.Pages;

public partial class Movies : ComponentBase
{
    [Inject]
    private IMovieService MovieService { get; set; } = default!;

    private List<Movie> movies = new();
    private int totalMovies = 0;
    private bool isLoading = true;
    private bool isLoadingMore = false;
    private int currentPage = 0;
    private const int PageSize = 20;
    private bool isModalOpen = false;
    private Movie? selectedMovie = null;

    protected override async Task OnInitializedAsync()
    {
        await LoadMovies();
    }

    private async Task LoadMovies()
    {
        try
        {
            isLoading = true;
            totalMovies = await MovieService.GetTotalMovieCountAsync();
            movies = await MovieService.GetMoviesAsync(0, PageSize);
            currentPage = 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading movies: {ex.Message}");
            movies = new List<Movie>();
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadMoreMovies()
    {
        try
        {
            isLoadingMore = true;
            var startIndex = currentPage * PageSize;
            var moreMovies = await MovieService.GetMoviesAsync(startIndex, PageSize);
            movies.AddRange(moreMovies);
            currentPage++;
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
