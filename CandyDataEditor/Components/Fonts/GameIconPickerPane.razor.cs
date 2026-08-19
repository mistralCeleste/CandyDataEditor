// Components/GameIconPickerPane.razor.cs
using CandyDataEditor;
using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Components.Fonts
{
    public partial class GameIconPickerPane : ComponentBase
    {
        [Inject] protected SqliteEditorConfig Config { get; set; } = default!;

        [Parameter] public string SearchTerm { get; set; } = string.Empty;
        [Parameter] public string MaxHeight { get; set; } = "380px";
        [Parameter] public EventCallback<string> OnIconSelected { get; set; }

        protected string searchTerm = string.Empty;

        protected override void OnParametersSet()
        {
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                searchTerm = SearchTerm;
            }
        }

        protected async Task SelectIconAsync(string iconTag)
        {
            if (OnIconSelected.HasDelegate)
            {
                await OnIconSelected.InvokeAsync(iconTag);
            }
        }

        public int FilteredCount => FilteredIcons.Count;

        protected List<string> ActiveIcons => Config?.DetectedLigatures != null && Config.DetectedLigatures.Any()
            ? Config.DetectedLigatures
            : DefaultFallbackIcons;

        protected List<string> FilteredIcons => string.IsNullOrWhiteSpace(searchTerm)
            ? ActiveIcons
            : ActiveIcons.Where(i => i.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        protected static readonly List<string> DefaultFallbackIcons = new()
        {
        };
    }
}
