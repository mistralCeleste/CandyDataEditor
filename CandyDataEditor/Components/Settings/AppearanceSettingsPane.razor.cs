// Components/AppearanceSettingsPane.razor.cs
using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CandyDataEditor.Components.Settings
{
    public partial class AppearanceSettingsPane : ComponentBase
    {
        [Inject] protected SqliteDataService DbService { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

        protected bool isDarkMode = false;

        protected override async Task OnInitializedAsync()
        {
            isDarkMode = DbService.Config.IsDarkMode;
            await ToggleThemeAsync(isDarkMode);
        }

        protected async Task ToggleThemeAsync(bool dark)
        {
            isDarkMode = dark;
            DbService.Config.IsDarkMode = dark;
            await JSRuntime.InvokeVoidAsync("document.body.setAttribute", "data-bs-theme", dark ? "dark" : "light");
        }
    }
}