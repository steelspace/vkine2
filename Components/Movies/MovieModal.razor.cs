using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using vkine.Models;
using vkine.Services;

namespace vkine.Components.Movies;

public partial class MovieModal : ComponentBase
{
    [Inject] private IScheduleService ScheduleService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IStringLocalizer<MovieModal> Localizer { get; set; } = default!;

    [Parameter]
    public bool IsOpen { get; set; }

    [Parameter]
    public Movie? Movie { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    [Parameter]
    public DateOnly? DateFrom { get; set; }

    [Parameter]
    public DateOnly? DateTo { get; set; }

    [Parameter]
    public TimeOnly? TimeFrom { get; set; }

    private bool HasActiveFilters => DateFrom.HasValue || DateTo.HasValue || TimeFrom.HasValue;

    private string? YouTubeVideoId => ExtractYouTubeVideoId(Movie?.TrailerUrl);

    private static string? ExtractYouTubeVideoId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var match = Regex.Match(url,
            @"(?:youtube\.com/(?:watch\?.*v=|embed/)|youtu\.be/)([\w-]{11})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string FilterSummary
    {
        get
        {
            var parts = new List<string>();
            if (DateFrom.HasValue && DateTo.HasValue)
            {
                parts.Add($"{DateFrom.Value:MMM d} – {DateTo.Value:MMM d, yyyy}");
            }
            if (TimeFrom.HasValue)
            {
                var formattedTime = TimeFrom.Value.ToString("HH:mm", CultureInfo.CurrentCulture);
                parts.Add(Localizer["FilterTimeFormat", formattedTime]);
            }
            return string.Join(", ", parts);
        }
    }

    private ElementReference backdropRef;
    private List<ScheduleDto>? schedules;
    private Dictionary<int, VenueDto> venues = new();
    private bool isLoadingSchedules = false;

    private bool _wasOpen;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsOpen && Movie != null)
        {
            try { await backdropRef.FocusAsync(); } catch { }
            if (!_wasOpen)
            {
                _wasOpen = true;
                await JS.InvokeVoidAsync("vkineMovie.lockScroll");
            }
        }
        else if (_wasOpen)
        {
            _wasOpen = false;
            await JS.InvokeVoidAsync("vkineMovie.unlockScroll");
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen && Movie != null && Movie.Id > 0)
        {
            await LoadSchedulesAsync();
        }
        else if (!IsOpen)
        {
            schedules = null;
            venues.Clear();
        }
    }

    private async Task LoadSchedulesAsync()
    {
        try
        {
            isLoadingSchedules = true;
            schedules = await ScheduleService.GetUpcomingPerformancesForMovieAsync(Movie!.Id);

            if (DateFrom.HasValue && DateTo.HasValue)
            {
                schedules = schedules
                    .Where(s => s.Date >= DateFrom.Value && s.Date <= DateTo.Value)
                    .ToList();
            }

            if (TimeFrom.HasValue)
            {
                foreach (var schedule in schedules)
                {
                    foreach (var performance in schedule.Performances)
                    {
                        performance.Showtimes = performance.Showtimes
                            .Where(st => st.StartAt >= TimeFrom.Value)
                            .ToList();
                    }

                    schedule.Performances = schedule.Performances
                        .Where(p => p.Showtimes.Count > 0)
                        .ToList();
                }

                schedules = schedules
                    .Where(s => s.Performances.Count > 0)
                    .ToList();
            }

            if (schedules != null && schedules.Count > 0)
            {
                var venueIds = schedules
                    .SelectMany(s => s.Performances)
                    .Select(p => p.VenueId)
                    .Distinct()
                    .ToList();

                venues = await ScheduleService.GetVenuesByIdsAsync(venueIds);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading schedules: {ex.Message}");
            schedules = new List<ScheduleDto>();
        }
        finally
        {
            isLoadingSchedules = false;
        }
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e is not null && (e.Key == "Escape" || e.Key == "Esc"))
        {
            await OnClose.InvokeAsync();
        }
    }
}
