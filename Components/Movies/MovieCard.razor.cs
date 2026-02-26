using Microsoft.AspNetCore.Components;
using vkine.Models;
using vkine.Services;

namespace vkine.Components.Movies;

public partial class MovieCard : ComponentBase
{
    [Inject] private ICountryLookupService CountryLookup { get; set; } = default!;

    [Parameter, EditorRequired]
    public Movie Movie { get; set; } = default!;

    [Parameter]
    public bool LazyLoad { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }
}
