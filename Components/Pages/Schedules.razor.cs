using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using vkine.Models;

namespace vkine.Components.Pages;

public partial class Schedules : ComponentBase
{
    private readonly Dictionary<int, Movie> movieDetails = new();
    private List<ScheduleSummary>? allSummaries;
    private List<ScheduleSummary>? visibleSummaries;
    private string searchQuery = string.Empty;
    private CancellationTokenSource? searchCts;
    private List<BadgeOption> badgeOptions = new();
    private HashSet<string> activeBadgeCodes = new(StringComparer.OrdinalIgnoreCase);
    private int? selectedMovieId;
    private Dictionary<DateOnly, List<Schedule>>? selectedSchedules;
    private Movie? selectedMovie;

    protected override async Task OnInitializedAsync()
    {
        allSummaries = await MovieService.GetTodaysSchedules();
        await EnsureMovieDetailsAsync(allSummaries?.Select(summary => summary.MovieId));
        UpdateBadgeFilters(allSummaries);
        ApplyFilters();
    }

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        searchQuery = e?.Value?.ToString() ?? string.Empty;

        searchCts?.Cancel();
        searchCts?.Dispose();
        searchCts = new CancellationTokenSource();
        var token = searchCts.Token;

        try
        {
            await Task.Delay(350, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await PerformSearchAsync(searchQuery);
        searchCts?.Dispose();
        searchCts = null;
    }

    private async Task PerformSearchAsync(string query)
    {
        allSummaries = await MovieService.SearchTodaysSchedules(query);
        await EnsureMovieDetailsAsync(allSummaries?.Select(summary => summary.MovieId));
        UpdateBadgeFilters(allSummaries);
        ApplyFilters();
    }

    private void OnBadgeToggle(string code, ChangeEventArgs e)
    {
        var isChecked = IsCheckboxChecked(e);

        if (isChecked)
        {
            activeBadgeCodes.Add(code);
        }
        else
        {
            activeBadgeCodes.Remove(code);
        }

        ApplyFilters();
    }

    private async Task EnsureMovieDetailsAsync(IEnumerable<int>? movieIds)
    {
        if (movieIds == null)
        {
            return;
        }

        var candidates = movieIds
            .Where(id => id > 0 && !movieDetails.ContainsKey(id))
            .Distinct()
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var fetched = await MovieService.GetMoviesByIdsAsync(candidates);

        foreach (var kvp in fetched)
        {
            movieDetails[kvp.Key] = kvp.Value;
        }
    }

    private Movie? LookupMovie(int movieId)
    {
        if (movieDetails.TryGetValue(movieId, out var movie))
        {
            return movie;
        }

        return null;
    }

    private async Task OnCardKeyDown(KeyboardEventArgs args, int movieId)
    {
        if (args == null)
        {
            return;
        }

        if (args.Key.Equals("Enter", StringComparison.OrdinalIgnoreCase) ||
            args.Key.Equals(" ", StringComparison.Ordinal) ||
            args.Key.Equals("Space", StringComparison.OrdinalIgnoreCase))
        {
            await HandleScheduleActivation(movieId);
        }
    }

    private async Task HandleScheduleActivation(int movieId)
    {
        if (selectedMovieId == movieId)
        {
            CloseScheduleModal();
            return;
        }

        await OpenScheduleModal(movieId);
    }

    private async Task OpenScheduleModal(int movieId)
    {
        var fetched = await MovieService.GetUpcomingSchedulesForMovie(movieId);

        if (fetched == null || fetched.Count == 0)
        {
            CloseScheduleModal();
            return;
        }

        selectedMovieId = movieId;
        selectedSchedules = fetched;
        await EnsureMovieDetailsAsync(new[] { movieId });
        selectedMovie = LookupMovie(movieId);
    }

    private void CloseScheduleModal()
    {
        selectedMovieId = null;
        selectedSchedules = null;
        selectedMovie = null;
    }

    private static bool IsCheckboxChecked(ChangeEventArgs args)
    {
        return args?.Value switch
        {
            bool boolean => boolean,
            string text when text.Equals("on", StringComparison.OrdinalIgnoreCase) => true,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => false
        };
    }

    private void UpdateBadgeFilters(List<ScheduleSummary>? dataset)
    {
        var discovered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (dataset != null)
        {
            foreach (var summary in dataset)
            {
                foreach (var badge in summary.Badges)
                {
                    if (string.IsNullOrWhiteSpace(badge.Code))
                    {
                        continue;
                    }

                    if (!discovered.ContainsKey(badge.Code))
                    {
                        discovered[badge.Code] = string.IsNullOrWhiteSpace(badge.Label)
                            ? badge.Code
                            : badge.Label;
                    }
                }
            }
        }

        if (!discovered.Any())
        {
            badgeOptions.Clear();
            activeBadgeCodes.Clear();
            return;
        }

        if (activeBadgeCodes.Count == 0)
        {
            foreach (var code in discovered.Keys)
            {
                activeBadgeCodes.Add(code);
            }
        }
        else
        {
            activeBadgeCodes.RemoveWhere(code => !discovered.ContainsKey(code));
            foreach (var code in discovered.Keys)
            {
                if (!activeBadgeCodes.Contains(code))
                {
                    activeBadgeCodes.Add(code);
                }
            }
        }

        badgeOptions = discovered
            .Select(kvp => new BadgeOption { Code = kvp.Key, Label = kvp.Value })
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ApplyFilters()
    {
        if (allSummaries == null)
        {
            visibleSummaries = null;
            CloseScheduleModal();
            return;
        }

        if (!badgeOptions.Any() || activeBadgeCodes.Count == badgeOptions.Count)
        {
            visibleSummaries = allSummaries;
            EnsureSelectedScheduleStillVisible();
            return;
        }

        var disallowed = new HashSet<string>(badgeOptions.Select(option => option.Code), StringComparer.OrdinalIgnoreCase);
        disallowed.ExceptWith(activeBadgeCodes);

        var filtered = allSummaries
            .Where(summary =>
            {
                if (summary.ShowtimeBadgeSignatures == null || !summary.ShowtimeBadgeSignatures.Any())
                {
                    return true;
                }

                return summary.ShowtimeBadgeSignatures.Any(signature =>
                    signature.Badges.All(code => !disallowed.Contains(code)));
            })
            .ToList();

        visibleSummaries = filtered;
        EnsureSelectedScheduleStillVisible();
    }

    private void EnsureSelectedScheduleStillVisible()
    {
        if (!selectedMovieId.HasValue)
        {
            return;
        }

        if (visibleSummaries == null)
        {
            CloseScheduleModal();
            return;
        }

        var summary = visibleSummaries.FirstOrDefault(s => s.MovieId == selectedMovieId.Value);

        if (summary == null)
        {
            CloseScheduleModal();
            return;
        }

        selectedMovie = LookupMovie(summary.MovieId);
    }

    private static string BuildFriendlyDateLabel(DateOnly day)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (day == today)
        {
            return "Today";
        }

        if (day == today.AddDays(1))
        {
            return "Tomorrow";
        }

        return day.DayOfWeek.ToString();
    }

    private static string FormatIsoDate(DateOnly day)
    {
        return day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string BuildFallbackLabel(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "?";
        }

        var trimmed = title.Trim();

        if (trimmed.Length == 0)
        {
            return "?";
        }

        var first = char.ToUpperInvariant(trimmed[0]);
        char? second = null;

        for (var i = 1; i < trimmed.Length && !second.HasValue; i++)
        {
            if (char.IsWhiteSpace(trimmed[i - 1]) && char.IsLetterOrDigit(trimmed[i]))
            {
                second = char.ToUpperInvariant(trimmed[i]);
            }
        }

        return second.HasValue ? $"{first}{second.Value}" : first.ToString();
    }

    private static string BuildBackdropStyle(Movie? movie)
    {
        if (movie == null || string.IsNullOrWhiteSpace(movie.BackdropUrl))
        {
            return string.Empty;
        }

        var safeUrl = movie.BackdropUrl.Replace("'", "%27", StringComparison.Ordinal);
        return $"background-image: url('{safeUrl}')";
    }

    private sealed class BadgeOption
    {
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
    }
}
