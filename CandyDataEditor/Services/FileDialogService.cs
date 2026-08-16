// Services/FileDialogService.cs
using CommunityToolkit.Maui.Storage;

namespace CandyDataEditor.Services;

public class FileDialogService
{
    /// <summary>
    /// Opens the native OS file picker to select an existing SQLite database file.
    /// </summary>
    public async Task<string?> SelectDatabaseFileDialogAsync()
    {
        try
        {
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".db", ".sqlite", ".sqlite3", ".db3" } },
                { DevicePlatform.MacCatalyst, new[] { "db", "sqlite", "sqlite3" } },
                { DevicePlatform.Android, new[] { "application/x-sqlite3", "application/octet-stream" } },
                { DevicePlatform.iOS, new[] { "public.data" } }
            });

            var options = new PickOptions
            {
                PickerTitle = "Select SQLite Database File",
                FileTypes = customFileType
            };

            var result = await FilePicker.Default.PickAsync(options);
            return result?.FullPath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Displays a native OS "Save As" file dialog to save or copy the database file to a specified path.
    /// </summary>
    /// <param name="defaultFileName">The default file name proposed in the dialog.</param>
    /// <param name="sourceFilePath">Path to the source file to copy to the chosen destination.</param>
    /// <returns>The destination file path if saved, or null if canceled.</returns>
    public async Task<string?> SelectFileDialogAsync(string defaultFileName = "game_data_copy.db", string? sourceFilePath = null)
    {
        try
        {
            using Stream stream = !string.IsNullOrEmpty(sourceFilePath) && File.Exists(sourceFilePath)
                ? (Stream)File.OpenRead(sourceFilePath)
                : new MemoryStream();

            var fileSaverResult = await FileSaver.Default.SaveAsync(defaultFileName, stream, CancellationToken.None);

            if (fileSaverResult.IsSuccessful)
            {
                return fileSaverResult.FilePath;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Opens the native OS folder picker to choose a target directory (e.g. for bulk exports).
    /// </summary>
    public async Task<string?> SelectExportFolderDialogAsync()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (result.IsSuccessful)
            {
                return result.Folder.Path;
            }
            return null;
        }
        catch (Exception)
        {
            return Path.Combine(FileSystem.AppDataDirectory, "Exports");
        }
    }
}