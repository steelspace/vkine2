using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using vkine.Models;
using vkine.Services;

namespace vkine.Components.Pages;

public partial class Movies : ComponentBase, IDisposable, IAsyncDisposable
{
    [Inject]
    private IMovieService MovieService { get; set; } = default!;

    [Inject]
    private IScheduleService ScheduleService { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    private bool isModalOpen = false;
    private Movie? selectedMovie = null;
    private bool isLoading = true;

    // Search state
    private string searchQuery = string.Empty;
    private List<Movie> searchResults = new();
    private bool isSearching = false;
    private CancellationTokenSource? _searchCts;

    // All movie IDs (ordered by earliest showtime) — loaded once
    private List<int> _allMovieIds = new();
    // Loaded movie data, keyed by ID
    private readonly Dictionary<int, Movie> _loadedMovies = new();

    private ElementReference _gridRef;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<Movies>? _dotnetRef;
    private bool _jsInitialized;

    protected override async Task OnInitializedAsync()
    {
        // Load all scheduled movie IDs at once (just integers — lightweight)
        _allMovieIds = await ScheduleService.GetMovieIdsWithUpcomingPerformancesAsync(0, int.MaxValue);
        isLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Initialize JS observer once the grid is actually in the DOM
        if (!_jsInitialized && !isLoading && _allMovieIds.Count > 0)
        {
            _jsInitialized = true;
            _dotnetRef = DotNetObjectReference.Create(this);
            _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./Components/Pages/Movies.razor.js");
            await _jsModule.InvokeVoidAsync("observeCards", _gridRef, _dotnetRef);
        }
    }

    /// <summary>
    /// Called from JS when cards scroll into view.
    /// Receives a batch of movie IDs whose data should be fetched.
    /// </summary>
    [JSInvokable]
    public async Task OnCardsVisible(int[] movieIds)
    {
        // Only fetch IDs we haven't loaded yet
        var toLoad = movieIds.Where(id => !_loadedMovies.ContainsKey(id)).ToList();
        if (toLoad.Count == 0) return;

        var fetched = await MovieService.GetMoviesByIdsAsync(toLoad);
        foreach (var kvp in fetched)
        {
            _loadedMovies[kvp.Key] = kvp.Value;
        }

        StateHasChanged();
    }

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        searchQuery = e?.Value?.ToString() ?? string.Empty;

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(750, token);

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                searchResults.Clear();
                StateHasChanged();
                return;
            }

            isSearching = true;
            StateHasChanged();

            var results = await MovieService.SearchMoviesAsync(searchQuery, 50);

            if (!token.IsCancellationRequested)
            {
                searchResults = results;
            }
        }
        catch (TaskCanceledException) { }
        finally
        {
            isSearching = false;
            StateHasChanged();
        }
    }

    private async Task OnKeyDownSearch(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                isSearching = true;
                StateHasChanged();
                var results = await MovieService.SearchMoviesAsync(searchQuery, 100);
                if (!token.IsCancellationRequested)
                {
                    searchResults = results;
                }
            }
            finally
            {
                isSearching = false;
                StateHasChanged();
            }
        }
    }

    private Movie? GetLoadedMovie(int movieId) =>
        _loadedMovies.TryGetValue(movieId, out var movie) ? movie : null;

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _dotnetRef?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("dispose");
                await _jsModule.DisposeAsync();
            }
            catch { }
        }
        _dotnetRef?.Dispose();
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
