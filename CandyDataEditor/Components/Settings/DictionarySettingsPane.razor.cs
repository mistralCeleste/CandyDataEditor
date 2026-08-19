// Components/DictionarySettingsPane.razor.cs
using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Diagnostics;

namespace CandyDataEditor.Components.Settings
{
    public partial class DictionarySettingsPane : ComponentBase
    {
        [Inject] protected GameDictionaryService DictionaryService { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

        protected bool isLoading = true;
        protected string dictDirectoryPath = string.Empty;
        protected List<DictionaryFileInfo> loadedDictFiles = new();
        protected List<string> activeDictionaryWords = new();
        protected string dictSearchQuery = string.Empty;
        protected string newWordInput = string.Empty;

        public class DictionaryFileInfo
        {
            public string FileName { get; set; } = string.Empty;
            public string FullPath { get; set; } = string.Empty;
            public int WordCount { get; set; }
            public bool IsLoaded { get; set; } = false;
        }

        protected IEnumerable<string> FilteredDictionaryWords =>
            string.IsNullOrWhiteSpace(dictSearchQuery)
                ? activeDictionaryWords
                : activeDictionaryWords.Where(w => w.Contains(dictSearchQuery, StringComparison.OrdinalIgnoreCase));

        protected override async Task OnInitializedAsync()
        {
            await RefreshDictionaryStateAsync();
        }

        public async Task RefreshDictionaryStateAsync()
        {
            isLoading = true;
            dictDirectoryPath = DictionaryService.GetLocalDictionariesFolder();

            // 1. Discover all .txt files upfront to populate the table with loading spinners immediately
            loadedDictFiles.Clear();
            if (Directory.Exists(dictDirectoryPath))
            {
                var files = Directory.GetFiles(dictDirectoryPath, "*.txt");
                foreach (var file in files)
                {
                    loadedDictFiles.Add(new DictionaryFileInfo
                    {
                        FileName = Path.GetFileName(file),
                        FullPath = file,
                        WordCount = 0,
                        IsLoaded = false
                    });
                }
            }

            StateHasChanged(); // Render the file list with spinners immediately

            // 2. Load dictionary content asynchronously with real-time per-file callback updates
            activeDictionaryWords = await DictionaryService.SyncAndLoadAllDictionariesAsync(
                (fileName, isFinished, count) =>
                {
                    var fileInfo = loadedDictFiles.FirstOrDefault(f => f.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                    if (fileInfo != null)
                    {
                        fileInfo.IsLoaded = isFinished;
                        fileInfo.WordCount = count;

                        // Force UI refresh on the UI thread as each file completes
                        InvokeAsync(StateHasChanged);
                    }
                });

            // 3. Sync to JS once all files are loaded
            try
            {
                await JSRuntime.InvokeVoidAsync("setGlobalGameDictionary", activeDictionaryWords);
            }
            catch (JSException ex)
            {
                Console.WriteLine($"JS Dict Sync Error: {ex.Message}");
            }
            finally
            {
                isLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        protected void OpenDictionaryDirectoryInExplorer()
        {
            if (Directory.Exists(dictDirectoryPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dictDirectoryPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
        }

        protected async Task HandleNewWordKeyUp(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                await AddNewWordAsync();
            }
        }

        protected async Task AddNewWordAsync()
        {
            if (string.IsNullOrWhiteSpace(newWordInput)) return;

            string word = newWordInput.Trim();
            if (!activeDictionaryWords.Contains(word, StringComparer.OrdinalIgnoreCase))
            {
                await DictionaryService.AddWordToDictionaryFileAsync("custom_user_words.txt", word);
                newWordInput = string.Empty;
                await RefreshDictionaryStateAsync();
            }
        }

        protected async Task RemoveWordAsync(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;

            await DictionaryService.RemoveWordFromAllDictionariesAsync(word);
            await RefreshDictionaryStateAsync();
        }
    }
}
