using Microsoft.JSInterop;

namespace CandyDataEditor.Components.Menus
{
    public partial class RibbonMenuBar
    {
        private string? openMenu = null;
        private string? activeSettingsTab = null;
        private string exportScope = "all";
        private bool isSettingsOpen = false;

        private void OpenSettings(string tabName)
        {
            activeSettingsTab = tabName;
            isSettingsOpen = true;
            openMenu = null;
        }

        private void ToggleMenu(string menuName)
        {
            openMenu = openMenu == menuName ? null : menuName;
        }

        private void CloseMenus()
        {
            openMenu = null;
        }

        private string ShortenPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= 35) return path;
            string root = Path.GetPathRoot(path) ?? "";
            string fileName = Path.GetFileName(path);
            return $"{root}...\\{fileName}";
        }

        private async Task OpenDatabaseFileDialog()
        {
            CloseMenus();

            string? selectedFilePath = await FileDialog.SelectDatabaseFileDialogAsync();

            if (!string.IsNullOrWhiteSpace(selectedFilePath) && File.Exists(selectedFilePath))
            {
                await DbService.SetDatabasePathAsync(selectedFilePath);
            }
        }

        private async Task SaveDatabaseAsDialog()
        {
            CloseMenus();

            string currentDbPath = DbService.CurrentDatabasePath;
            string defaultName = $"game_data_copy_{DateTime.Now:yyyyMMdd_HHmmss}.db";

            string? savedPath = await FileDialog.SelectFileDialogAsync(defaultName, currentDbPath);

            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                await JSRuntime.InvokeVoidAsync("alert", $"Database copy saved successfully to:\n{savedPath}");
            }
        }

        private async Task CloseCurrentDatabase()
        {
            CloseMenus();
            await DbService.CloseDatabaseAsync();
        }

        private async Task LoadRecentDatabase(string path)
        {
            CloseMenus();

            if (File.Exists(path))
            {
                await DbService.SetDatabasePathAsync(path);
            }
            else
            {
                DbService.Config.RemoveRecentDatabase(path);
            }
        }

        private void ExitApplication()
        {
            CloseMenus();
            Application.Current?.Quit();
        }

        private async Task ExportData(string format)
        {
            CloseMenus();

            string? exportFolder = await FileDialog.SelectExportFolderDialogAsync();
            if (string.IsNullOrEmpty(exportFolder)) return;

            var tables = await DbService.GetTablesAndViewsAsync();
            var filtered = tables
                .Where(t => exportScope == "all" || t.Type == exportScope)
                .Select(t => t.Name)
                .ToList();

            if (format == "html-cards")
            {
                await DbService.ExportHtmlCardsBundleAsync(filtered, exportFolder);
                await JSRuntime.InvokeVoidAsync("alert", $"HTML Cards Bundle exported to:\n{exportFolder}");
            }
            else
            {
                await DbService.ExportDataFilesAsync(filtered, format, exportFolder);
                await JSRuntime.InvokeVoidAsync("alert", $"Exported {filtered.Count} object(s) in {format.ToUpper()} format to:\n{exportFolder}");
            }
        }

        protected override async Task OnInitializedAsync()
        {
            DbService.OnDatabasePathChanged += HandleDatabaseChanged;
        }

        private async Task HandleDatabaseChanged(string newPath)
        {
            StateHasChanged();
        }

        public void Dispose()
        {
            DbService.OnDatabasePathChanged -= HandleDatabaseChanged;
        }
    }
}