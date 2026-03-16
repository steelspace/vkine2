using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
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
    private IStringLocalizer<Movies> Localizer { get; set; } = default!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

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
    private bool _isDraggingTimeSlider;

    // Time-of-day filter (minutes from midnight; 540 = 9:00 = off / leftmost)
    private int _timeFromMinutes = 540;
    private bool _timeChipOpen = false;

    // Sort state
    private enum SortField { None, Rating, Name, ReleaseDate }
    private SortField _currentSort = SortField.Rating;
    private bool _sortAscending = false;
    private List<int>? _unsortedMovieIds;

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
    private bool _sessionStorageChecked;

    protected override async Task OnInitializedAsync()
    {
        var urlQuery = new Uri(NavigationManager.Uri).Query;

        if (!string.IsNullOrEmpty(urlQuery))
        {
            ParseQueryString(urlQuery);
            _sessionStorageChecked = true; // URL has params — no need to check sessionStorage
        }
        // else: _sessionStorageChecked = false, OnAfterRenderAsync will read sessionStorage

        // If state was restored from prerender, skip the DB call
        if (AllMovieIds.Count == 0)
        {
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
        }

        // Restore search results if query is in the URL
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            _unfilteredSearchResults = await MovieService.SearchMoviesAsync(searchQuery, 50);
            await ApplySearchFilters();
        }

        // Apply sort — requires all movie data
        if (_currentSort != SortField.None)
        {
            _unsortedMovieIds = AllMovieIds.ToList();
            await EnsureAllMoviesLoadedAsync();
            SortMovieIds();
        }

        if (_sessionStorageChecked)
            isLoading = false;
        // else: OnAfterRenderAsync will read sessionStorage then set isLoading = false
    }

    private void ParseQueryString(string queryString)
    {
        var query = QueryHelpers.ParseQuery(queryString);

        if (query.TryGetValue("q", out var q) && !string.IsNullOrEmpty(q))
            searchQuery = q.ToString();

        if (query.TryGetValue("sort", out var sort) &&
            Enum.TryParse<SortField>(sort, ignoreCase: true, out var sortField))
            _currentSort = sortField;

        _sortAscending = query.TryGetValue("asc", out var asc) && asc == "true";

        if (query.TryGetValue("time", out var time) &&
            int.TryParse(time, out var timeMinutes) && timeMinutes > TimeSliderMin)
            _timeFromMinutes = timeMinutes;

        if (query.TryGetValue("from", out var from) && DateOnly.TryParse(from, out var dateFrom))
            _dateFrom = dateFrom;

        if (query.TryGetValue("to", out var to) && DateOnly.TryParse(to, out var dateTo))
            _dateTo = dateTo;
    }

    private async Task UpdateUrl()
    {
        var @params = new Dictionary<string, object?>();

        if (!string.IsNullOrEmpty(searchQuery))
            @params["q"] = searchQuery;

        // Omit sort params only when they match the default (Rating, descending)
        if (_currentSort != SortField.Rating || _sortAscending)
            @params["sort"] = _currentSort.ToString().ToLowerInvariant();

        if (_sortAscending)
            @params["asc"] = "true";

        if (_timeFromMinutes > TimeSliderMin)
            @params["time"] = _timeFromMinutes.ToString();

        if (_dateFrom.HasValue)
            @params["from"] = _dateFrom.Value.ToString("yyyy-MM-dd");

        if (_dateTo.HasValue)
            @params["to"] = _dateTo.Value.ToString("yyyy-MM-dd");

        var url = NavigationManager.GetUriWithQueryParameters(@params);
        var queryString = new Uri(url).Query;
        await JSRuntime.InvokeVoidAsync("history.replaceState", (object?)null, "", url);
        await JSRuntime.InvokeVoidAsync("sessionStorage.setItem", "vkine-movies-filters", queryString);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Restore filters from sessionStorage when navigating back (no URL params on return)
        if (!_sessionStorageChecked)
        {
            _sessionStorageChecked = true;
            var stored = await JSRuntime.InvokeAsync<string?>("sessionStorage.getItem", "vkine-movies-filters");
            if (!string.IsNullOrEmpty(stored))
            {
                ParseQueryString(stored);
                await ApplyFilters();
            }
            isLoading = false;
            StateHasChanged();
            return;
        }

        // Initialize JS module early so sticky toolbar + date picker can be set up
        if (!_datePickerInitialized && !isLoading)
        {
            _datePickerInitialized = true;
            _dotnetRef ??= DotNetObjectReference.Create(this);
            _jsModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./Components/Pages/Movies.razor.js");
            var initialDates = _dateFrom.HasValue
                ? new[] { _dateFrom.Value.ToString("yyyy-MM-dd"), (_dateTo ?? _dateFrom).Value.ToString("yyyy-MM-dd") }
                : null;
            await _jsModule.InvokeVoidAsync("initDateRangePicker", _dateRangeInput, _dotnetRef, initialDates);
            await _jsModule.InvokeVoidAsync("initStickyToolbar", _toolbarRef);
            await _jsModule.InvokeVoidAsync("initTimeSlider");
        }
        if (_timeChipOpen && _jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("initChipTimeSlider");
        }
        if (!_jsInitialized && !isLoading && AllMovieIds.Count > 0 && string.IsNullOrWhiteSpace(searchQuery))
        {
            _jsInitialized = true;
            _dotnetRef ??= DotNetObjectReference.Create(this);
            _jsModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./Components/Pages/Movies.razor.js");
            await _jsModule.InvokeVoidAsync("observeCards", _gridRef, _dotnetRef);
            await JSRuntime.InvokeVoidAsync("vkineMovie.restoreMoviesScroll");
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

    protected override bool ShouldRender() => !_isDraggingTimeSlider;

    private void OnTimeFromInput(ChangeEventArgs e)
    {
        _isDraggingTimeSlider = true;
        _timeFromMinutes = int.Parse(e.Value?.ToString() ?? "0");
    }

    private async Task OnTimeFromChanged(ChangeEventArgs e)
    {
        _isDraggingTimeSlider = false;
        _timeFromMinutes = int.Parse(e.Value?.ToString() ?? "0");
        await ApplyFilters();
    }

    private void ToggleTimeChip() => _timeChipOpen = !_timeChipOpen;

    private async Task ClearTimeFilter()
    {
        _timeFromMinutes = TimeSliderMin;
        _timeChipOpen = false;
        await ApplyFilters();
    }

    private async Task OpenDatePickerChip()
    {
        if (_jsModule is not null)
            await _jsModule.InvokeVoidAsync("openDatePicker");
    }

    private async Task CycleSort()
    {
        var sequence = new (SortField field, bool asc)[]
        {
            (SortField.Rating, false),
            (SortField.Rating, true),
            (SortField.Name, true),
            (SortField.Name, false),
            (SortField.ReleaseDate, false),
            (SortField.ReleaseDate, true),
        };
        var idx = Array.FindIndex(sequence, x => x.field == _currentSort && x.asc == _sortAscending);
        var next = sequence[(idx + 1) % sequence.Length];
        _currentSort = next.field;
        _sortAscending = next.asc;
        await ApplySortingAsync();
    }

    private string DateChipLabel
    {
        get
        {
            if (!_dateFrom.HasValue) return Localizer["DateRange"];
            var culture = CultureInfo.CurrentUICulture;
            if (!_dateTo.HasValue || _dateFrom == _dateTo)
                return _dateFrom.Value.ToString("MMM d", culture);
            if (_dateFrom.Value.Month == _dateTo.Value.Month && _dateFrom.Value.Year == _dateTo.Value.Year)
                return $"{_dateFrom.Value.ToString("MMM d", culture)}–{_dateTo.Value.Day}";
            return $"{_dateFrom.Value.ToString("MMM d", culture)}–{_dateTo.Value.ToString("MMM d", culture)}";
        }
    }

    internal const int TimeSliderMin = 540; // 9:00

    private string TimeFromLabel => _timeFromMinutes <= TimeSliderMin
        ? Localizer["TimeSliderOff"]
        : TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(_timeFromMinutes)).ToString("HH:mm", CultureInfo.CurrentCulture);

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
        await UpdateUrl();
        StateHasChanged();
    }

    private async Task ClearSearch()
    {
        searchQuery = string.Empty;
        searchResults.Clear();
        _unfilteredSearchResults.Clear();
        _searchCts?.Cancel();
        _jsInitialized = false;
        await UpdateUrl();
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
                await UpdateUrl();
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
                await UpdateUrl();
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
                    await UpdateUrl();
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
            await UpdateUrl();
            StateHasChanged();
            return;
        }

        // Save unsorted order
        _unsortedMovieIds ??= AllMovieIds.ToList();

        await EnsureAllMoviesLoadedAsync();
        SortMovieIds();

        _jsInitialized = false;
        await UpdateUrl();
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

    private static string GetSortTitle(Movie? movie)
    {
        if (movie is null) return "\uffff";
        var isCzech = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "cs";
        return isCzech
            ? (string.IsNullOrEmpty(movie.Title) ? movie.TitleEn : movie.Title)
            : (string.IsNullOrEmpty(movie.TitleEn) ? movie.Title : movie.TitleEn);
    }

    private List<(int Id, Movie? Movie)> ApplySort(List<(int Id, Movie? Movie)> items) => (_currentSort switch
    {
        SortField.Rating => _sortAscending
            ? items.OrderBy(x => x.Movie is not null ? CalculateAverageRating(x.Movie) : -1)
            : items.OrderByDescending(x => x.Movie is not null ? CalculateAverageRating(x.Movie) : -1),
        SortField.Name => _sortAscending
            ? items.OrderBy(x => GetSortTitle(x.Movie), StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: true))
            : items.OrderByDescending(x => GetSortTitle(x.Movie), StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: true)),
        SortField.ReleaseDate => _sortAscending
            ? items.OrderBy(x => ParseFirstYear(x.Movie?.Year, 9999))
                   .ThenByDescending(x => x.Movie is not null ? CalculateAverageRating(x.Movie) : -1)
            : items.OrderByDescending(x => ParseFirstYear(x.Movie?.Year, 0))
                   .ThenByDescending(x => x.Movie is not null ? CalculateAverageRating(x.Movie) : -1),
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

    private async Task OpenModal(Movie movie)
    {
        var isMobile = await JSRuntime.InvokeAsync<bool>("eval", "window.innerWidth <= 768");
        if (isMobile)
        {
            var queryParams = new Dictionary<string, object?>();
            if (_dateFrom.HasValue) queryParams["from"] = _dateFrom.Value.ToString("yyyy-MM-dd");
            if (_dateTo.HasValue) queryParams["to"] = _dateTo.Value.ToString("yyyy-MM-dd");
            if (_timeFromMinutes > TimeSliderMin) queryParams["time"] = _timeFromMinutes.ToString();
            var url = NavigationManager.GetUriWithQueryParameters($"/movie/{movie.Id}", queryParams);
            await JSRuntime.InvokeVoidAsync("vkineMovie.saveMoviesScroll");
            NavigationManager.NavigateTo(url);
        }
        else
        {
            selectedMovie = movie;
            isModalOpen = true;
        }
    }

    private void CloseModal()
    {
        isModalOpen = false;
        selectedMovie = null;
    }
}
