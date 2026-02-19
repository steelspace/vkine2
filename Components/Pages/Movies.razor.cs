using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
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

    [Inject]
    private ILogger<Movies> Logger { get; set; } = default!;

    // Theme state
    private bool _appliedIsDark;

    private bool isModalOpen = false;
    private Movie? selectedMovie = null;
    private bool isLoading = true;

    // Search state
    private string searchQuery = string.Empty;
    private List<Movie> searchResults = new();
    private List<Movie> _unfilteredSearchResults = new();
    private bool isSearching = false;
    private CancellationTokenSource? _searchCts;

    // Date range filter
    private DateOnly? _dateFrom;
    private DateOnly? _dateTo;
    private bool _datePickerInitialized;
    private ElementReference _dateRangeInput;
    private bool _isDateFiltering;

    // Time-of-day filter (minutes from midnight; 540 = 9:00 = off / leftmost)
    private int _timeFromMinutes = 540;

    // Sort state
    private enum SortField { None, Rating, Name, ReleaseDate }
    private SortField _currentSort = SortField.Rating;
    private bool _sortAscending = false;
    private List<int>? _unsortedMovieIds;
    private bool _isSortLoading;

    // All movie IDs (ordered by earliest showtime) — loaded once, persisted across prerender
    [PersistentState]
    public List<int> AllMovieIds { get => field ??= []; set; }
    // Loaded movie data, keyed by ID
    private readonly Dictionary<int, Movie> _loadedMovies = new();

    private ElementReference _gridRef;
    private ElementReference _toolbarRef;
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

        // Apply default sort (rating descending) — requires all movie data
        if (_currentSort != SortField.None)
        {
            _unsortedMovieIds = AllMovieIds.ToList();
            await EnsureAllMoviesLoadedAsync();
            SortMovieIds();
        }

        isLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Initialize JS module early so sticky toolbar + date picker can be set up
        if (!_datePickerInitialized && !isLoading)
        {
            _datePickerInitialized = true;
            _dotnetRef ??= DotNetObjectReference.Create(this);
            _jsModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./Components/Pages/Movies.razor.js");
            await _jsModule.InvokeVoidAsync("initDateRangePicker", _dateRangeInput, _dotnetRef);
            await _jsModule.InvokeVoidAsync("initStickyToolbar", _toolbarRef);
            await _jsModule.InvokeVoidAsync("initTimeSlider");
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

        // Initialize theme state on first render
        if (firstRender)
        {
            try
            {
                var applied = await JSRuntime.InvokeAsync<string>("eval",
                    "document.documentElement.getAttribute('data-theme') || 'light'");
                _appliedIsDark = string.Equals(applied, "dark", StringComparison.OrdinalIgnoreCase);
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to read theme state.");
            }
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
        await ApplyFilters();
    }

    private async Task ClearDateRange()
    {
        _dateFrom = null;
        _dateTo = null;

        if (_jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("clearDateRange");
        }

        await ApplyFilters();
    }

    private async Task OnTimeFromChanged(ChangeEventArgs e)
    {
        _timeFromMinutes = int.Parse(e.Value?.ToString() ?? "0");
        await ApplyFilters();
    }

    internal const int TimeSliderMin = 540; // 9:00

    private string TimeFromLabel => _timeFromMinutes <= TimeSliderMin
        ? "Off"
        : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(_timeFromMinutes)).ToString("HH:mm");

    private TimeOnly? TimeFromValue => _timeFromMinutes > TimeSliderMin
        ? TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(_timeFromMinutes))
        : null;

    /// <summary>
    /// Returns the set of movie IDs matching the current date-range + time-from filters,
    /// or null if no schedule filters are active.
    /// </summary>
    private async Task<HashSet<int>?> GetActiveFilteredMovieIdsAsync()
    {
        if (!_dateFrom.HasValue && !_dateTo.HasValue && _timeFromMinutes <= TimeSliderMin)
            return null;

        List<int> ids;
        if (_dateFrom.HasValue && _dateTo.HasValue)
        {
            ids = await ScheduleService.GetMovieIdsInDateRangeAsync(
                _dateFrom.Value, _dateTo.Value, TimeFromValue);
        }
        else
        {
            ids = await ScheduleService.GetMovieIdsWithUpcomingPerformancesAsync(
                0, int.MaxValue, TimeFromValue);
        }

        return ids.ToHashSet();
    }

    /// <summary>
    /// Filters the unfiltered search results by the current schedule filters.
    /// </summary>
    private async Task ApplySearchFilters()
    {
        var allowedIds = await GetActiveFilteredMovieIdsAsync();
        if (allowedIds is not null)
        {
            searchResults = _unfilteredSearchResults
                .Where(m => allowedIds.Contains(m.Id))
                .ToList();
        }
        else
        {
            searchResults = _unfilteredSearchResults.ToList();
        }

        // Re-apply sort if active
        if (_currentSort != SortField.None)
        {
            SortMovieList(searchResults);
        }
    }

    /// <summary>
    /// Re-fetches movie IDs using the current combination of date-range + time-from filters.
    /// Also re-filters active text search results.
    /// </summary>
    private async Task ApplyFilters()
    {
        _isDateFiltering = true;
        StateHasChanged();

        if (_dateFrom.HasValue && _dateTo.HasValue)
        {
            AllMovieIds = await ScheduleService.GetMovieIdsInDateRangeAsync(
                _dateFrom.Value, _dateTo.Value, TimeFromValue);
        }
        else
        {
            AllMovieIds = await ScheduleService.GetMovieIdsWithUpcomingPerformancesAsync(
                0, int.MaxValue, TimeFromValue);
        }

        // If a text search is active, re-filter its results too
        if (!string.IsNullOrWhiteSpace(searchQuery) && _unfilteredSearchResults.Count > 0)
        {
            await ApplySearchFilters();
        }

        // Re-apply sort if active
        if (_currentSort != SortField.None)
        {
            _unsortedMovieIds = AllMovieIds.ToList();
            await EnsureAllMoviesLoadedAsync();
            SortMovieIds();
        }

        _jsInitialized = false;
        _isDateFiltering = false;
        StateHasChanged();
    }

    private void ClearSearch()
    {
        searchQuery = string.Empty;
        searchResults.Clear();
        _unfilteredSearchResults.Clear();
        _searchCts?.Cancel();
        _jsInitialized = false;
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
                _unfilteredSearchResults.Clear();
                _jsInitialized = false; // grid will re-enter the DOM, observer must re-attach
                StateHasChanged();
                return;
            }

            isSearching = true;
            StateHasChanged();

            var results = await MovieService.SearchMoviesAsync(searchQuery, 50);

            if (!token.IsCancellationRequested)
            {
                _unfilteredSearchResults = results;
                await ApplySearchFilters();
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
                    _unfilteredSearchResults = results;
                    await ApplySearchFilters();
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

    // ── Sorting ────────────────────────────────────────────────────

    private static bool GetDefaultAscending(SortField field) => field == SortField.Name;

    private async Task ToggleSort(SortField field)
    {
        if (_currentSort == field)
        {
            // Same button clicked: flip direction
            _sortAscending = !_sortAscending;
        }
        else
        {
            // Different button: switch field, keep current direction
            _currentSort = field;
        }

        await ApplySortingAsync();
    }

    private async Task ApplySortingAsync()
    {
        if (_currentSort == SortField.None) return;

        // Sort search results
        if (!string.IsNullOrWhiteSpace(searchQuery) && searchResults.Count > 0)
        {
            SortMovieList(searchResults);
            StateHasChanged();
            return;
        }

        // Save unsorted order
        _unsortedMovieIds ??= AllMovieIds.ToList();

        _isSortLoading = true;
        StateHasChanged();

        await EnsureAllMoviesLoadedAsync();
        SortMovieIds();

        _isSortLoading = false;
        _jsInitialized = false;
        StateHasChanged();
    }

    private void SortMovieIds()
    {
        if (_currentSort == SortField.None) return;

        var items = AllMovieIds
            .Select(id => (Id: id, Movie: GetLoadedMovie(id)))
            .ToList();

        AllMovieIds = ApplySort(items)
            .Select(x => x.Id)
            .ToList();
    }

    private void SortMovieList(List<Movie> movies)
    {
        if (_currentSort == SortField.None) return;

        var items = movies
            .Select(m => (Id: m.Id, Movie: (Movie?)m))
            .ToList();

        var sorted = ApplySort(items)
            .Select(x => x.Movie!)
            .ToList();

        movies.Clear();
        movies.AddRange(sorted);
    }

    private List<(int Id, Movie? Movie)> ApplySort(List<(int Id, Movie? Movie)> items) => (_currentSort switch
    {
        SortField.Rating => _sortAscending
            ? items.OrderBy(x => x.Movie is not null ? CalculateAverageRating(x.Movie) : -1)
            : items.OrderByDescending(x => x.Movie is not null ? CalculateAverageRating(x.Movie) : -1),
        SortField.Name => _sortAscending
            ? items.OrderBy(x => x.Movie?.Title ?? "\uffff", StringComparer.OrdinalIgnoreCase)
            : items.OrderByDescending(x => x.Movie?.Title ?? "", StringComparer.OrdinalIgnoreCase),
        SortField.ReleaseDate => _sortAscending
            ? items.OrderBy(x => ParseFirstYear(x.Movie?.Year, 9999))
            : items.OrderByDescending(x => ParseFirstYear(x.Movie?.Year, 0)),
        _ => items.AsEnumerable()
    }).ToList();

    /// <summary>
    /// Extracts the first 4-digit year from a year string (e.g. "2023", "2023–2025", "2023-2025").
    /// Returns <paramref name="fallback"/> when no year can be parsed.
    /// </summary>
    private static int ParseFirstYear(string? year, int fallback)
    {
        if (string.IsNullOrEmpty(year)) return fallback;

        var match = System.Text.RegularExpressions.Regex.Match(year, @"\d{4}");
        return match.Success && int.TryParse(match.Value, out var y) ? y : fallback;
    }

    /// <summary>
    /// Calculates the average rating across all available rating sources (ČSFD, TMDB, IMDb),
    /// normalized to a 0–10 scale.
    /// </summary>
    private static double CalculateAverageRating(Movie movie)
    {
        var count = 0;
        var sum = 0.0;

        // ČSFD: percentage string like "78%" → 7.8
        if (!string.IsNullOrEmpty(movie.CsfdRating))
        {
            var raw = movie.CsfdRating.TrimEnd('%', ' ');
            if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var csfd))
            {
                sum += csfd / 10.0;
                count++;
            }
        }

        // TMDB: 0–10
        if (movie.TmdbRating is > 0)
        {
            sum += movie.TmdbRating.Value;
            count++;
        }

        // IMDb: 0–10
        if (movie.ImdbRating is > 0)
        {
            sum += movie.ImdbRating.Value;
            count++;
        }

        return count > 0 ? sum / count : 0;
    }

    private async Task EnsureAllMoviesLoadedAsync()
    {
        var missing = AllMovieIds.Where(id => !_loadedMovies.ContainsKey(id)).ToList();
        if (missing.Count == 0) return;

        var fetched = await MovieService.GetMoviesByIdsAsync(missing);
        foreach (var kvp in fetched)
        {
            _loadedMovies[kvp.Key] = kvp.Value;
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

    private async Task ToggleTheme()
    {
        try
        {
            await JSRuntime.InvokeAsync<string>("vkineTheme.toggle");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Theme toggle failed, using DOM fallback.");
            try
            {
                await JSRuntime.InvokeVoidAsync("eval",
                    @"(function(){var el=document.documentElement; el.setAttribute('data-theme', el.getAttribute('data-theme') === 'dark' ? 'light' : 'dark');})();");
            }
            catch { }
        }
        finally
        {
            try
            {
                var applied = await JSRuntime.InvokeAsync<string>("eval",
                    "document.documentElement.getAttribute('data-theme') || 'light'");
                _appliedIsDark = string.Equals(applied, "dark", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
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
