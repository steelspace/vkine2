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

    // Date range filter
    private DateOnly? _dateFrom;
    private DateOnly? _dateTo;
    private bool _datePickerInitialized;
    private ElementReference _dateRangeInput;
    private bool _isDateFiltering;

    // All movie IDs (ordered by earliest showtime) — loaded once, persisted across prerender
    [PersistentState]
    public List<int> AllMovieIds { get => field ??= []; set; }
    // Loaded movie data, keyed by ID
    private readonly Dictionary<int, Movie> _loadedMovies = new();

    private ElementReference _gridRef;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<Movies>? _dotnetRef;
    private bool _jsInitialized;

    protected override async Task OnInitializedAsync()
    {
        // If state was restored from prerender, skip the DB call
        if (AllMovieIds.Count == 0)
        {
            AllMovieIds = await ScheduleService.GetMovieIdsWithUpcomingPerformancesAsync(0, int.MaxValue);
        }

        isLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Initialize the Flatpickr date range picker once, after first render when the input is in the DOM
        if (!_datePickerInitialized && !isLoading)
        {
            _datePickerInitialized = true;
            _dotnetRef ??= DotNetObjectReference.Create(this);
            _jsModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./Components/Pages/Movies.razor.js");
            await _jsModule.InvokeVoidAsync("initDateRangePicker", _dateRangeInput, _dotnetRef);
        }

        // (Re-)initialize JS observer whenever the grid is in the DOM but not yet observed
        if (!_jsInitialized && !isLoading && AllMovieIds.Count > 0 && string.IsNullOrWhiteSpace(searchQuery))
        {
            _jsInitialized = true;
            _dotnetRef ??= DotNetObjectReference.Create(this);
            _jsModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
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

    /// <summary>
    /// Called from JS when the user picks a date range via Flatpickr.
    /// </summary>
    [JSInvokable]
    public async Task OnDateRangeChanged(string from, string to)
    {
        _dateFrom = DateOnly.Parse(from);
        _dateTo = DateOnly.Parse(to);
        _isDateFiltering = true;
        StateHasChanged();

        // Fetch movie IDs in the selected date range
        AllMovieIds = await ScheduleService.GetMovieIdsInDateRangeAsync(_dateFrom.Value, _dateTo.Value);

        // Reset observer so new placeholders get observed
        _jsInitialized = false;
        _isDateFiltering = false;
        StateHasChanged();
    }

    private async Task ClearDateRange()
    {
        _dateFrom = null;
        _dateTo = null;

        if (_jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("clearDateRange");
        }

        _isDateFiltering = true;
        StateHasChanged();

        // Reload all upcoming movie IDs
        AllMovieIds = await ScheduleService.GetMovieIdsWithUpcomingPerformancesAsync(0, int.MaxValue);

        _jsInitialized = false;
        _isDateFiltering = false;
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
                _jsInitialized = false; // grid will re-enter the DOM, observer must re-attach
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
