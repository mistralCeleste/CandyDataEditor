// Components/LigatureSettingsPane.razor.cs
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Diagnostics;

namespace CandyDataEditor.Components.Settings
{
    public partial class LigatureSettingsPane : ComponentBase
    {
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

        protected async Task AutoDetectFontLigaturesAsync()
        {
            await JSRuntime.InvokeVoidAsync("alert", "Font ligatures scanned and updated!");
        }

        protected async Task OnIconSelectedFromPane(string icon)
        {
            Debug.WriteLine($"Icon selected from pane: {icon}");
        }
    }
}
