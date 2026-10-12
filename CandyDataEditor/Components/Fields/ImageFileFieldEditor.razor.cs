// Fields/ImageFileFieldEditor.razor.cs
using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Components.Fields
{
    public partial class ImageFileFieldEditor : ComponentBase
    {
        [Inject] protected FileDialogService FileDialog { get; set; } = default!;
        [Inject] protected SqliteDataService DbService { get; set; } = default!;

        [Parameter] public string Value { get; set; } = string.Empty;
        [Parameter] public EventCallback<string> ValueChanged { get; set; }
        [Parameter] public bool IsReadOnly { get; set; } = false;

        protected string resolvedAbsolutePath = string.Empty;
        protected string imageDataUri = string.Empty;

        protected bool FileExists => !string.IsNullOrWhiteSpace(resolvedAbsolutePath) && File.Exists(resolvedAbsolutePath);

        protected string FileExtension => !string.IsNullOrWhiteSpace(resolvedAbsolutePath)
            ? Path.GetExtension(resolvedAbsolutePath).TrimStart('.').ToUpper()
            : string.Empty;

        protected bool IsDirectRenderableImage => new[] { ".png", ".jpg", ".jpeg", ".svg" }
            .Contains(Path.GetExtension(resolvedAbsolutePath).ToLower());

        protected override void OnParametersSet()
        {
            resolvedAbsolutePath = FileDialog.ResolveToAbsolutePath(Value, DbService.Config, DbService.CurrentDatabasePath);
            LoadImageDataUri();
        }

        protected async Task BrowseFileAsync()
        {
            if (IsReadOnly) return;

            string? startDir = null;
            if (!string.IsNullOrWhiteSpace(resolvedAbsolutePath) && FileExists)
            {
                startDir = Path.GetDirectoryName(resolvedAbsolutePath);
            }
            else if (!string.IsNullOrWhiteSpace(DbService.CurrentDatabasePath))
            {
                startDir = Path.GetDirectoryName(DbService.CurrentDatabasePath);
            }

            string? pickedPath = await FileDialog.SelectExternalImageFileAsync(startDir);
            if (!string.IsNullOrWhiteSpace(pickedPath))
            {
                string formatted = FileDialog.FormatPathByConfig(pickedPath, DbService.Config, DbService.CurrentDatabasePath);
                Value = formatted;
                await ValueChanged.InvokeAsync(formatted);
            }
        }

        private void LoadImageDataUri()
        {
            imageDataUri = string.Empty;

            if (FileExists && IsDirectRenderableImage)
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(resolvedAbsolutePath);
                    string ext = Path.GetExtension(resolvedAbsolutePath).TrimStart('.').ToLower();
                    if (ext == "svg") ext = "svg+xml";

                    imageDataUri = $"data:image/{ext};base64,{Convert.ToBase64String(fileBytes)}";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading image file for thumbnail: {ex.Message}");
                }
            }
        }

        protected async Task ClearFile()
        {
            if (IsReadOnly) return;
            Value = string.Empty;
            await ValueChanged.InvokeAsync(string.Empty);
        }

        protected string ShortenPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= 40) return path;
            string fileName = Path.GetFileName(path);
            return $"...\\{fileName}";
        }
    }
}
