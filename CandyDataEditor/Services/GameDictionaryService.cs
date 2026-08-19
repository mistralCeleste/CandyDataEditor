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
    /// Gets the physical disk path to the wwwroot/dictionaries folder in the app package.
    /// </summary>
    public string GetBundledDictionariesDirectory()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "dictionaries");
    }

    /// <summary>
    /// Gets the active writeable dictionaries folder in AppData.
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
    /// Asynchronously scans, copies missing bundled dictionaries, and reads all words with per-file status reporting.
    /// </summary>
    public async Task<List<string>> SyncAndLoadAllDictionariesAsync(Action<string, bool, int>? onFileProcessed = null)
    {
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
                    string fileName = Path.GetFileName(filePath);
                    onFileProcessed?.Invoke(fileName, false, 0);

                    var lines = await File.ReadAllLinesAsync(filePath, System.Text.Encoding.UTF8);
                    int fileWordCount = 0;

                    foreach (var line in lines)
                    {
                        var word = line.Trim();
                        if (!string.IsNullOrWhiteSpace(word) && !word.StartsWith("#"))
                        {
                            allWords.Add(word);
                            fileWordCount++;
                        }
                    }

                    onFileProcessed?.Invoke(fileName, true, fileWordCount);
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

    /// <summary>
    /// Scans ALL active dictionary files in AppData and strips the word wherever it appears.
    /// </summary>
    public async Task RemoveWordFromAllDictionariesAsync(string wordToRemove)
    {
        if (string.IsNullOrWhiteSpace(wordToRemove)) return;

        string targetWord = wordToRemove.Trim();
        string localFolder = GetLocalDictionariesFolder();

        if (!Directory.Exists(localFolder)) return;

        var dictFiles = Directory.GetFiles(localFolder, "*.txt");

        foreach (var filePath in dictFiles)
        {
            try
            {
                var lines = (await File.ReadAllLinesAsync(filePath, System.Text.Encoding.UTF8)).ToList();

                int removedCount = lines.RemoveAll(l => l.Trim().Equals(targetWord, StringComparison.OrdinalIgnoreCase));

                if (removedCount > 0)
                {
                    await File.WriteAllLinesAsync(filePath, lines, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing word from {filePath}: {ex.Message}");
            }
        }
    }

    public void RaiseOnDictionaryLoaded(List<string> wordList)
    {
        OnDictionaryLoaded?.Invoke(wordList);
    }
}
