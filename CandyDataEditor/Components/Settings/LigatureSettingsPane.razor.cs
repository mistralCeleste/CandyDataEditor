// Components/LigatureSettingsTab.razor.cs
using System.IO;
using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Components.Settings
{
    public partial class LigatureSettingsPane : ComponentBase
    {
        [Inject] protected FontLigatureService FontService { get; set; } = default!;
        [Inject] protected GameDictionaryService DictionaryService { get; set; } = default!;

        [Parameter] public EventCallback<string> OnLigatureSelected { get; set; }

        protected string fontFilePath = string.Empty;
        protected List<string> detectedLigatures = new();
        protected bool isScanning = false;
        protected bool hasScanned = false;
        protected string? errorMessage = null;

        protected override void OnInitialized()
        {
            // Default font path check inside wwwroot or AppData
            string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "fonts", "GameIcons.ttf");
            if (File.Exists(defaultPath))
            {
                fontFilePath = defaultPath;
            }
        }

        protected async Task AutoDetectFontLigaturesAsync()
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(fontFilePath))
            {
                errorMessage = "Please enter a valid font file path.";
                return;
            }

            if (!File.Exists(fontFilePath))
            {
                errorMessage = $"Font file not found at path: {fontFilePath}";
                return;
            }

            isScanning = true;
            StateHasChanged();

            // Run font parsing on background thread to keep Blazor UI smooth
            detectedLigatures = await Task.Run(() => FontService.ExtractLigatures(fontFilePath, "icon"));

            isScanning = false;
            hasScanned = true;
            StateHasChanged();
        }

        protected async Task SelectLigature(string ligature)
        {
            if (OnLigatureSelected.HasDelegate)
            {
                await OnLigatureSelected.InvokeAsync(ligature);
            }
        }

        protected async Task OnIconSelectedFromPane(string iconName)
        {
            string formattedLigature = $"[{iconName}]";
            await SelectLigature(formattedLigature);
        }
    }
}
