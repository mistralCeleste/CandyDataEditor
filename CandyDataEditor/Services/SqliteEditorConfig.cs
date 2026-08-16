// Models/SqliteEditorConfig.cs
using System.Runtime.CompilerServices;

namespace CandyDataEditor;

public class SqliteEditorConfig
{
    // Default Fallbacks
    private const int DefaultQueryRowLimit = 100;
    private const int DefaultSmallCharThreshold = 50;
    private const int DefaultMediumCharThreshold = 150;
    private const int DefaultLargeCharThreshold = 300;
    private const bool DefaultIsDarkMode = false;

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
}