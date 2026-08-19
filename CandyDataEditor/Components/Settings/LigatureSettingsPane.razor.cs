// Components/Panes/LigatureSettingsPane.razor.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace CandyDataEditor.Components.Settings
{
    public partial class LigatureSettingsPane : ComponentBase
    {
        [Inject] protected SqliteEditorConfig Config { get; set; } = default!;
        [Inject] protected FontLigatureService FontService { get; set; } = default!;

        protected bool isScanning = false;
        protected string? errorMessage;
        protected string? statusMessage;

        protected void SaveConfigState()
        {
            Config.FontFilePath = Config.FontFilePath;
            Config.FontFeatureTable = Config.FontFeatureTable;
        }

        protected async Task BrowseFontFileAsync()
        {
            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.font", "ttf", "otf" } },
                        { DevicePlatform.Android, new[] { "font/ttf", "font/otf", "application/x-font-ttf", "application/x-font-opentype" } },
                        { DevicePlatform.WinUI, new[] { ".ttf", ".otf" } },
                        { DevicePlatform.MacCatalyst, new[] { "ttf", "otf" } }
                    });

                var options = new PickOptions
                {
                    PickerTitle = "Select Game Icon Font",
                    FileTypes = customFileType
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    Config.FontFilePath = result.FullPath;
                    statusMessage = "Font file selected.";
                    errorMessage = null;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"File picker error: {ex.Message}";
            }
        }

        protected async Task ScanFontLigaturesAsync()
        {
            errorMessage = null;
            statusMessage = null;

            if (string.IsNullOrWhiteSpace(Config.FontFilePath))
            {
                errorMessage = "Please enter or pick a font file path first.";
                return;
            }

            if (!File.Exists(Config.FontFilePath))
            {
                errorMessage = $"Font file not found on disk: {Config.FontFilePath}";
                return;
            }

            string featureTable = string.IsNullOrWhiteSpace(Config.FontFeatureTable) ? "liga" : Config.FontFeatureTable;

            isScanning = true;
            StateHasChanged();

            try
            {
                string path = Config.FontFilePath;
                List<string> ligatures = await Task.Run(() => FontService.ExtractLigatures(path, featureTable));

                Config.DetectedLigatures = ligatures;
                statusMessage = $"Successfully scanned and saved {ligatures.Count} ligatures to configuration!";
            }
            catch (Exception ex)
            {
                errorMessage = $"Scan failed: {ex.Message}";
            }
            finally
            {
                isScanning = false;
                StateHasChanged();
            }
        }

        protected Task OnIconSelectedFromSettings(string iconTag)
        {
            // Optional: Copy ligature string "[iconTag]" to clipboard or set status message
            statusMessage = $"Selected icon tag: [{iconTag}]";
            return Task.CompletedTask;
        }
    }
}
