using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using vkine.Models;
using vkine.Services;

namespace vkine.Components.Pages;

public partial class MovieDetail : ComponentBase
{
    [Inject] private IMovieService MovieService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public int Id { get; set; }

    [SupplyParameterFromQuery(Name = "from")] [Parameter] public string? FromStr { get; set; }
    [SupplyParameterFromQuery(Name = "to")]   [Parameter] public string? ToStr { get; set; }
    [SupplyParameterFromQuery(Name = "time")] [Parameter] public string? TimeStr { get; set; }

    private Movie? _movie;
    private bool _loading = true;
    private DateOnly? _dateFrom;
    private DateOnly? _dateTo;
    private TimeOnly? _timeFrom;

    protected override async Task OnInitializedAsync()
    {
        if (DateOnly.TryParse(FromStr, out var dateFrom)) _dateFrom = dateFrom;
        if (DateOnly.TryParse(ToStr, out var dateTo)) _dateTo = dateTo;
        if (int.TryParse(TimeStr, out var timeMinutes) && timeMinutes > 540)
            _timeFrom = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(timeMinutes));

        var movies = await MovieService.GetMoviesByIdsAsync([Id]);
        _movie = movies.GetValueOrDefault(Id);
        _loading = false;
    }

    private async Task GoBack()
    {
        await JS.InvokeVoidAsync("history.back");
    }
}
