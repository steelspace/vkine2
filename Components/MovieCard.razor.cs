using Microsoft.AspNetCore.Components;
using vkine.Models;

namespace vkine.Components;

public partial class MovieCard : ComponentBase
{
    [Parameter, EditorRequired]
    public Movie Movie { get; set; } = default!;

    [Parameter]
    public bool LazyLoad { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }
}
