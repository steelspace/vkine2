using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using vkine.Models;
using vkine.Services;

namespace vkine.Components.Pages;

public partial class Premieres : ComponentBase
{
    [Inject] private IPremiereService PremiereService { get; set; } = default!;
    [Inject] private IMovieService MovieService { get; set; } = default!;
    [Inject] private IStringLocalizer<Premieres> Localizer { get; set; } = default!;

    private bool isLoading = true;
    private bool isModalOpen;
    private Movie? selectedMovie;
    private int totalCount;

    private List<PremiereGroup> groupedPremieres = [];

    protected override async Task OnInitializedAsync()
    {
        var premieres = await PremiereService.GetUpcomingPremieresAsync();

        if (premieres.Count > 0)
        {
            var csfdIds = premieres.Select(p => p.CsfdId).Distinct().ToList();
            var movies = await MovieService.GetMoviesByIdsAsync(csfdIds);

            groupedPremieres = premieres
                .Where(p => movies.ContainsKey(p.CsfdId))
                .GroupBy(p => p.PremiereDateOnly)
                .Select(g => new PremiereGroup
                {
                    Date = g.Key,
                    Movies = g
                        .Select(p => movies[p.CsfdId])
                        .ToList()
                })
                .OrderBy(g => g.Date)
                .ToList();

            totalCount = groupedPremieres.Sum(g => g.Movies.Count);
        }

        isLoading = false;
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

    private sealed class PremiereGroup
    {
        public DateOnly Date { get; init; }
        public List<Movie> Movies { get; init; } = [];
    }
}
