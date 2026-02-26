using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace vkine.Components.Shared;

public partial class ThemeToggle : ComponentBase
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private bool _isDark;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                var applied = await JSRuntime.InvokeAsync<string>("eval",
                    "document.documentElement.getAttribute('data-theme') || 'light'");
                _isDark = string.Equals(applied, "dark", StringComparison.OrdinalIgnoreCase);
                StateHasChanged();
            }
            catch { }
        }
    }

    private async Task Toggle()
    {
        try
        {
            await JSRuntime.InvokeAsync<string>("vkineTheme.toggle");
        }
        catch
        {
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
                _isDark = string.Equals(applied, "dark", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }
    }
}
