using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace vkine.Components.Layout;

public partial class MainLayout : LayoutComponentBase
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private ILogger<MainLayout> Logger { get; set; } = default!;

    private string CurrentMode { get; set; } = "system"; // 'system' | 'light' | 'dark'
    private bool AppliedIsDark { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                // read persisted mode from JS
                var mode = await JSRuntime.InvokeAsync<string>("vkineTheme.get");
                CurrentMode = string.IsNullOrEmpty(mode) ? "system" : mode;

                // read currently applied theme (light|dark) from document attribute
                var applied = await JSRuntime.InvokeAsync<string>("eval", "document.documentElement.getAttribute('data-theme') || 'light'");
                AppliedIsDark = string.Equals(applied, "dark", StringComparison.OrdinalIgnoreCase);

                StateHasChanged();

                // keep existing listener (no-op) so page doesn't error when theme.js dispatches events
                await JSRuntime.InvokeVoidAsync("eval", @"(function(){document.addEventListener('vkine-theme-changed', e => { /* noop */ });})()");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to initialize theme state from JavaScript.");
            }
        }
    }

    private async Task ToggleTheme()
    {
        try
        {
            // preferred path: call the theme helper that persists mode + applies it
            var next = await JSRuntime.InvokeAsync<string>("vkineTheme.toggle");
            CurrentMode = next ?? CurrentMode;
        }
        catch (JSException jsEx)
        {
            // fallback if the vkineTheme API isn't available for any reason
            Logger.LogWarning(jsEx, "vkineTheme.toggle not available — attempting DOM fallback.");
            try
            {
                await JSRuntime.InvokeVoidAsync("eval", @"(function(){var el=document.documentElement; el.setAttribute('data-theme', el.getAttribute('data-theme') === 'dark' ? 'light' : 'dark');})();");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Fallback theme toggle failed.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error toggling theme.");
        }
        finally
        {
            try
            {
                var applied = await JSRuntime.InvokeAsync<string>("eval", "document.documentElement.getAttribute('data-theme') || 'light'");
                AppliedIsDark = string.Equals(applied, "dark", StringComparison.OrdinalIgnoreCase);
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not read applied theme after toggle.");
            }
        }
    }
}
