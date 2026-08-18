// Services/GameDictionaryService.cs
namespace CandyDataEditor.Services;

public class GameDictionaryService
{
    public event Action<List<string>>? OnDictionaryLoaded;

    private readonly HttpClient _httpClient;

    public int WordCount;

    public GameDictionaryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets the physical disk path to the wwwroot/dictionaries folder.
    /// </summary>
    public string GetBundledDictionariesDirectory()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "dictionaries");
    }

    /// <summary>
    /// Gets a safe writeable folder in AppDataDirectory.
    /// </summary>
    public string GetLocalDictionariesFolder()
    {
        string dir = Path.Combine(FileSystem.AppDataDirectory, "dictionaries");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    /// <summary>
    /// Asynchronously scans, copies missing bundled dictionaries, and reads all words on a background thread.
    /// </summary>
    public async Task<List<string>> SyncAndLoadAllDictionariesAsync()
    {
        // Run file scanning and text parsing off the UI thread
        return await Task.Run(async () =>
        {
            var allWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string bundledDir = GetBundledDictionariesDirectory();
                string localDir = GetLocalDictionariesFolder();

                if (Directory.Exists(bundledDir))
                {
                    var bundledTxtFiles = Directory.GetFiles(bundledDir, "*.txt");

                    foreach (var bundledFilePath in bundledTxtFiles)
                    {
                        string fileName = Path.GetFileName(bundledFilePath);
                        string targetLocalPath = Path.Combine(localDir, fileName);

                        if (!File.Exists(targetLocalPath))
                        {
                            File.Copy(bundledFilePath, targetLocalPath, overwrite: false);
                        }
                    }
                }

                var allLocalTxtFiles = Directory.GetFiles(localDir, "*.txt");

                foreach (var filePath in allLocalTxtFiles)
                {
                    // Asynchronous UTF-8 read
                    var lines = await File.ReadAllLinesAsync(filePath, System.Text.Encoding.UTF8);

                    foreach (var line in lines)
                    {
                        var word = line.Trim();
                        if (!string.IsNullOrWhiteSpace(word) && !word.StartsWith("#"))
                        {
                            allWords.Add(word);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Async Dictionary Load Error: {ex.Message}");
            }

            var wordList = allWords.ToList();
            WordCount = wordList.Count;
            RaiseOnDictionaryLoaded(wordList);
            return wordList;
        });
    }

    /// <summary>
    /// Appends a new custom word to a specific dictionary file in AppData.
    /// </summary>
    public async Task AddWordToDictionaryFileAsync(string fileName, string newWord)
    {
        if (string.IsNullOrWhiteSpace(newWord)) return;

        try
        {
            string fullPath = Path.Combine(GetLocalDictionariesFolder(), Path.GetFileName(fileName));

            var existingWords = File.Exists(fullPath)
                ? await File.ReadAllLinesAsync(fullPath, System.Text.Encoding.UTF8)
                : Array.Empty<string>();

            if (!existingWords.Contains(newWord.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                await File.AppendAllLinesAsync(fullPath, new[] { newWord.Trim() }, System.Text.Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save word '{newWord}' to {fileName}: {ex.Message}");
        }
    }

    public async Task RemoveWordFromDictionaryFileAsync(string fileName, string wordToRemove)
    {
        string filePath = Path.Combine(GetLocalDictionariesFolder(), fileName);
        if (!File.Exists(filePath)) return;

        var lines = (await File.ReadAllLinesAsync(filePath)).ToList();

        // Case-insensitive removal
        int removedCount = lines.RemoveAll(l => l.Trim().Equals(wordToRemove.Trim(), StringComparison.OrdinalIgnoreCase));

        if (removedCount > 0)
        {
            await File.WriteAllLinesAsync(filePath, lines);
        }
    }

    /// <summary>
    /// Seeds default dictionary files from wwwroot into AppData if they don't exist yet.
    /// </summary>
    public async Task EnsureDefaultDictionariesSeededAsync(params string[] fileNames)
    {
        string localFolder = GetLocalDictionariesFolder();

        foreach (var fileName in fileNames)
        {
            string targetPath = Path.Combine(localFolder, fileName);
            if (!File.Exists(targetPath))
            {
                try
                {
                    // Fetch default content from wwwroot/dictionaries/ via HttpClient
                    var content = await _httpClient.GetStringAsync($"dictionaries/{fileName}");
                    await File.WriteAllTextAsync(targetPath, content, System.Text.Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not seed {fileName}: {ex.Message}");
                }
            }
        }
    }

    public void RaiseOnDictionaryLoaded(List<string> wordList)
    {
        OnDictionaryLoaded?.Invoke(wordList);
    }
}