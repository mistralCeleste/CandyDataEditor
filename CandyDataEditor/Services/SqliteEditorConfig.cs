// Models/SqliteEditorConfig.cs
using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;

namespace CandyDataEditor;

public enum ExternalFilePathMode
{
    Absolute = 0,
    RelativeToRoot = 1,
    RelativeToDatabase = 2
}


public class SqliteEditorConfig
{
    // Default Fallbacks
    private const int DefaultQueryRowLimit = 100;
    private const int DefaultSmallCharThreshold = 50;
    private const int DefaultMediumCharThreshold = 150;
    private const int DefaultLargeCharThreshold = 300;
    private const bool DefaultIsDarkMode = false;
    private const int MaxRecentDatabases = 5;
    private const int DefaultFilePathMode = (int)ExternalFilePathMode.RelativeToDatabase;
    private const string DefaultFileRootPath = "";
    private const bool DefaultUsePosixPathSeparators = false;

    // Font Defaults
    private const string DefaultFontFilePath = "";
    private const string DefaultFontFeatureTable = "liga";

    public SqliteEditorConfig()
    {
        // Load settings from persistent storage on startup
        QueryRowLimit = GetStoredValue(nameof(QueryRowLimit), DefaultQueryRowLimit);
        SmallCharThreshold = GetStoredValue(nameof(SmallCharThreshold), DefaultSmallCharThreshold);
        MediumCharThreshold = GetStoredValue(nameof(MediumCharThreshold), DefaultMediumCharThreshold);
        LargeCharThreshold = GetStoredValue(nameof(LargeCharThreshold), DefaultLargeCharThreshold);
        IsDarkMode = GetStoredValue(nameof(IsDarkMode), DefaultIsDarkMode);

        FontFilePath = GetStoredValue(nameof(FontFilePath), DefaultFontFilePath);
        FontFeatureTable = GetStoredValue(nameof(FontFeatureTable), DefaultFontFeatureTable);
    }

    public int QueryRowLimit
    {
        get => GetStoredValue(nameof(QueryRowLimit), DefaultQueryRowLimit);
        set => SetStoredValue(nameof(QueryRowLimit), value);
    }

    public int SmallCharThreshold
    {
        get => GetStoredValue(nameof(SmallCharThreshold), DefaultSmallCharThreshold);
        set => SetStoredValue(nameof(SmallCharThreshold), value);
    }

    public int MediumCharThreshold
    {
        get => GetStoredValue(nameof(MediumCharThreshold), DefaultMediumCharThreshold);
        set => SetStoredValue(nameof(MediumCharThreshold), value);
    }

    public int LargeCharThreshold
    {
        get => GetStoredValue(nameof(LargeCharThreshold), DefaultLargeCharThreshold);
        set => SetStoredValue(nameof(LargeCharThreshold), value);
    }

    public bool IsDarkMode
    {
        get => GetStoredValue(nameof(IsDarkMode), DefaultIsDarkMode);
        set => SetStoredValue(nameof(IsDarkMode), value);
    }

    public string FontFilePath
    {
        get => GetStoredValue(nameof(FontFilePath), DefaultFontFilePath);
        set => SetStoredValue(nameof(FontFilePath), value);
    }

    public string FontFeatureTable
    {
        get => GetStoredValue(nameof(FontFeatureTable), DefaultFontFeatureTable);
        set => SetStoredValue(nameof(FontFeatureTable), value);
    }

    public List<string> DetectedLigatures
    {
        get => GetStoredList(nameof(DetectedLigatures));
        set => SetStoredList(nameof(DetectedLigatures), value);
    }

    public List<string> RecentDatabases
    {
        get => GetStoredList(nameof(RecentDatabases));
        private set => SetStoredList(nameof(RecentDatabases), value);
    }

    public ExternalFilePathMode FilePathMode
    {
        get => (ExternalFilePathMode)GetStoredValue(nameof(FilePathMode), DefaultFilePathMode);
        set => SetStoredValue(nameof(FilePathMode), (int)value);
    }

    public string FileRootPath
    {
        get => GetStoredValue(nameof(FileRootPath), DefaultFileRootPath);
        set => SetStoredValue(nameof(FileRootPath), value);
    }

    public bool UsePosixPathSeparators
    {
        get => GetStoredValue(nameof(UsePosixPathSeparators), DefaultUsePosixPathSeparators);
        set => SetStoredValue(nameof(UsePosixPathSeparators), value);
    }

    public void AddRecentDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var list = RecentDatabases;
        list.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);

        if (list.Count > MaxRecentDatabases)
        {
            list = list.Take(MaxRecentDatabases).ToList();
        }

        RecentDatabases = list;
    }

    public void RemoveRecentDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var list = RecentDatabases;
        if (list.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            RecentDatabases = list;
        }
    }

    public int GetLineCountForText(string text)
    {
        if (string.IsNullOrEmpty(text)) return 3;

        int len = text.Length;
        if (len <= SmallCharThreshold) return 3;
        if (len <= MediumCharThreshold) return 6;
        if (len <= LargeCharThreshold) return 10;

        return Math.Min(25, 12 + ((len - LargeCharThreshold) / 100));
    }

    // --- HELPER STORAGE METHODS USING MAUI PREFERENCES ---

    private int GetStoredValue(string key, int defaultValue)
    {
        return Preferences.Default.Get(key, defaultValue);
    }

    private bool GetStoredValue(string key, bool defaultValue)
    {
        return Preferences.Default.Get(key, defaultValue);
    }

    private string GetStoredValue(string key, string defaultValue)
    {
        return Preferences.Default.Get(key, defaultValue);
    }

    private void SetStoredValue<T>(string key, T value)
    {
        if (value is int intVal)
            Preferences.Default.Set(key, intVal);
        else if (value is bool boolVal)
            Preferences.Default.Set(key, boolVal);
        else if (value is string strVal)
            Preferences.Default.Set(key, strVal);
    }

    private List<string> GetStoredList(string key)
    {
        string json = Preferences.Default.Get(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private void SetStoredList(string key, List<string> list)
    {
        string json = JsonSerializer.Serialize(list ?? new List<string>());
        Preferences.Default.Set(key, json);
    }
}
