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

    private const int PageSize = 20;
    private bool isModalOpen = false;
    private Movie? selectedMovie = null;
    private bool isLoadingMore = false;

    // Search state
    private string searchQuery = string.Empty;
    private List<Movie> searchResults = new();
    private bool isSearching = false;
    private CancellationTokenSource? _searchCts;

    // Infinite scroll state
    private List<Movie> visibleMovies = new();
    private int _currentSkip = 0;
    private bool _hasMore = true;
    private ElementReference scrollContainer;
    private IJSObjectReference? _scrollModule;
    private DotNetObjectReference<Movies>? _dotnetRef;

    protected override async Task OnInitializedAsync()
    {
        await LoadMoreMovies();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotnetRef = DotNetObjectReference.Create(this);
            _scrollModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./Components/Pages/Movies.razor.js");
            await _scrollModule.InvokeVoidAsync("initializeScrollObserver", scrollContainer, _dotnetRef);
        }
    }

    [JSInvokable]
    public async Task OnScrollNearEnd()
    {
        if (!isLoadingMore && _hasMore && string.IsNullOrWhiteSpace(searchQuery))
        {
            await LoadMoreMovies();
        }
    }

    private async Task LoadMoreMovies()
    {
        if (!_hasMore || isLoadingMore) return;

        try
        {
            isLoadingMore = true;
            StateHasChanged();

            var ids = await ScheduleService.GetMovieIdsWithUpcomingPerformancesAsync(_currentSkip, PageSize);
            
            if (ids.Count == 0)
            {
                _hasMore = false;
                return;
            }

            _currentSkip += ids.Count;
            if (ids.Count < PageSize) _hasMore = false;

            var moviesById = await MovieService.GetMoviesByIdsAsync(ids);
            var pageMovies = ids.Where(id => moviesById.ContainsKey(id)).Select(id => moviesById[id]).ToList();

            visibleMovies.AddRange(pageMovies);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading more movies: {ex.Message}");
        }
        finally
        {
            isLoadingMore = false;
            StateHasChanged();
        }
    }

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        searchQuery = e?.Value?.ToString() ?? string.Empty;

        // debounce previous searches
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            // small debounce to emulate google-like instant search
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

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _dotnetRef?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_scrollModule != null)
        {
            try
            {
                await _scrollModule.InvokeVoidAsync("dispose");
                await _scrollModule.DisposeAsync();
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
