using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace vkine.Components.Layout;

public partial class MainLayout : LayoutComponentBase
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    private string CurrentMode { get; set; } = "system"; // 'system' | 'light' | 'dark'

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                // read persisted mode from JS
                var mode = await JSRuntime.InvokeAsync<string>("vkineTheme.get");
                CurrentMode = string.IsNullOrEmpty(mode) ? "system" : mode;
                StateHasChanged();

                // subscribe to theme change events so UI updates if theme changed externally
                await JSRuntime.InvokeVoidAsync("eval", @"(function(){document.addEventListener('vkine-theme-changed', e => { /* noop */ });})()");
            }
            catch { }
        }
    }

    private async Task ToggleTheme()
    {
        try
        {
            var next = await JSRuntime.InvokeAsync<string>("vkineTheme.toggle");
            CurrentMode = next ?? CurrentMode;
            StateHasChanged();
        }
        catch { }
    }
}
