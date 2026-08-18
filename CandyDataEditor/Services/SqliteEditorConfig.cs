// Models/SqliteEditorConfig.cs
using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;

namespace CandyDataEditor;

public class SqliteEditorConfig
{
    // Default Fallbacks
    private const int DefaultQueryRowLimit = 100;
    private const int DefaultSmallCharThreshold = 50;
    private const int DefaultMediumCharThreshold = 150;
    private const int DefaultLargeCharThreshold = 300;
    private const bool DefaultIsDarkMode = false;
    private const int MaxRecentDatabases = 5;

    public SqliteEditorConfig()
    {
        // Load settings from persistent storage on startup
        QueryRowLimit = GetStoredValue(nameof(QueryRowLimit), DefaultQueryRowLimit);
        SmallCharThreshold = GetStoredValue(nameof(SmallCharThreshold), DefaultSmallCharThreshold);
        MediumCharThreshold = GetStoredValue(nameof(MediumCharThreshold), DefaultMediumCharThreshold);
        LargeCharThreshold = GetStoredValue(nameof(LargeCharThreshold), DefaultLargeCharThreshold);
        IsDarkMode = GetStoredValue(nameof(IsDarkMode), DefaultIsDarkMode);
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

    public List<string> RecentDatabases
    {
        get => GetStoredList(nameof(RecentDatabases));
        private set => SetStoredList(nameof(RecentDatabases), value);
    }

    /// <summary>
    /// Adds a database path to the top of the recent list, removes duplicates, and caps at 5 entries.
    /// </summary>
    public void AddRecentDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var list = RecentDatabases;

        // Case-insensitive removal of existing entry to avoid duplicates
        list.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));

        // Insert at the top
        list.Insert(0, path);

        // Cap to most recent
        if (list.Count > MaxRecentDatabases)
        {
            list = list.Take(MaxRecentDatabases).ToList();
        }

        RecentDatabases = list;
    }

    /// <summary>
    /// Removes a file path if it no longer exists on disk or was deleted.
    /// </summary>
    public void RemoveRecentDatabase(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var list = RecentDatabases;
        if (list.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            RecentDatabases = list;
        }
    }

    /// <summary>
    /// Calculates how many lines of editor height to assign a string based on character thresholds.
    /// </summary>
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