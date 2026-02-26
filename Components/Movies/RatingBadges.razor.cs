using Microsoft.AspNetCore.Components;
using vkine.Models;

namespace vkine.Components.Movies;

public partial class RatingBadges : ComponentBase
{
    [Parameter, EditorRequired]
    public Movie Movie { get; set; } = default!;

    [Parameter]
    public bool Compact { get; set; }
}
