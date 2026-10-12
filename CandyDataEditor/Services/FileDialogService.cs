// Services/FileDialogService.cs
using CommunityToolkit.Maui.Storage;

namespace CandyDataEditor.Services;

public class FileDialogService
{

    public static FilePickerFileType ImageAndDesignFileTypes => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.WinUI, new[] { ".png", ".jpg", ".jpeg", ".svg", ".ai", ".psd" } },
        { DevicePlatform.MacCatalyst, new[] { "png", "jpg", "jpeg", "svg", "ai", "psd" } },
        { DevicePlatform.Android, new[] { "image/png", "image/jpeg", "image/svg+xml", "application/octet-stream" } },
        { DevicePlatform.iOS, new[] { "public.image", "com.adobe.photoshop-image", "com.adobe.illustrator.ai-image" } }
    });

    public static FilePickerFileType DatabaseFileTypes => new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.WinUI, new[] { ".db", ".sqlite", ".sqlite3", ".db3" } },
        { DevicePlatform.MacCatalyst, new[] { "db", "sqlite", "sqlite3" } },
        { DevicePlatform.Android, new[] { "application/x-sqlite3", "application/octet-stream" } },
        { DevicePlatform.iOS, new[] { "public.data" } }
    });

    /// <summary>
    /// Opens the native OS file picker to select an existing SQLite database file.
    /// </summary>
    public async Task<string?> SelectDatabaseFileDialogAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select SQLite Database File",
                FileTypes = DatabaseFileTypes
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

    public async Task<string?> SelectExternalImageFileAsync(string? initialDirectory = null)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select Image / External Design File",
                FileTypes = ImageAndDesignFileTypes
            };

            var result = await FilePicker.Default.PickAsync(options);
            return result?.FullPath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public string FormatPathByConfig(string fullPath, SqliteEditorConfig config, string? dbPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;

        string resultPath = fullPath;
        string pathSeparator = config.UsePosixPathSeparators ? "/" : "\\";

        if (config.FilePathMode == ExternalFilePathMode.RelativeToDatabase && !string.IsNullOrWhiteSpace(dbPath))
        {
            string? dbFolder = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrWhiteSpace(dbFolder))
            {
                resultPath = Path.GetRelativePath(dbFolder, fullPath);
            }
        }
        else if (config.FilePathMode == ExternalFilePathMode.RelativeToRoot && !string.IsNullOrWhiteSpace(config.FileRootPath))
        {
            resultPath = Path.GetRelativePath(config.FileRootPath, fullPath);
        }

        if (config.UsePosixPathSeparators)
        {
            resultPath = resultPath.Replace('\\', '/');
        }
        else
        {
            resultPath = resultPath.Replace('/', '\\');
        }

        if (config.FilePathMode != ExternalFilePathMode.Absolute && !Path.IsPathFullyQualified(resultPath))
        {
            if (!resultPath.StartsWith(".") && !resultPath.StartsWith(".."))
            {
                resultPath = $".{pathSeparator}{resultPath}";
            }
        }

        return resultPath;
    }

    public string ResolveToAbsolutePath(string rawPath, SqliteEditorConfig config, string? dbPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        string normalized = rawPath.Replace('/', Path.DirectorySeparatorChar)
                                   .Replace('\\', Path.DirectorySeparatorChar);

        if (Path.IsPathFullyQualified(normalized))
        {
            return normalized;
        }

        if (config.FilePathMode == ExternalFilePathMode.RelativeToDatabase && !string.IsNullOrWhiteSpace(dbPath))
        {
            string? dbFolder = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrWhiteSpace(dbFolder))
            {
                return Path.GetFullPath(Path.Combine(dbFolder, normalized));
            }
        }
        else if (config.FilePathMode == ExternalFilePathMode.RelativeToRoot && !string.IsNullOrWhiteSpace(config.FileRootPath))
        {
            return Path.GetFullPath(Path.Combine(config.FileRootPath, normalized));
        }

        return normalized;
    }
}
