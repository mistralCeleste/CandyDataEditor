using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System.Diagnostics;


namespace CandyDataEditor.Components.Settings
{
    public partial class SettingsDialog : ComponentBase
    {
        [Inject] protected GameDictionaryService DictionaryService { get; set; } = default!;
        [Inject] protected SqliteDataService DbService { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;

        [Parameter] public bool IsOpen { get; set; }
        [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
        [Parameter] public string DefaultTab { get; set; } = "dictionary";

        protected bool isDarkMode = false;
        protected string activeTab = "dictionary";
        private bool previousIsOpenState = false;

        // Dictionary State Fields
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
        }

        protected IEnumerable<string> FilteredDictionaryWords =>
            string.IsNullOrWhiteSpace(dictSearchQuery)
                ? activeDictionaryWords
                : activeDictionaryWords.Where(w => w.Contains(dictSearchQuery, StringComparison.OrdinalIgnoreCase));

        protected override async Task OnInitializedAsync()
        {
            isDarkMode = DbService.Config.IsDarkMode;
            await ToggleTheme(isDarkMode);
        }

        protected override async Task OnParametersSetAsync()
        {
            // Only trigger reload when transitioning from Closed -> Open
            if (IsOpen && !previousIsOpenState)
            {
                activeTab = !string.IsNullOrEmpty(DefaultTab) ? DefaultTab : "dictionary";

                if (activeTab == "dictionary")
                {
                    await RefreshDictionaryStateAsync();
                }
            }

            previousIsOpenState = IsOpen;
        }

        protected async Task SetTabAsync(string tab)
        {
            activeTab = tab;
            if (tab == "dictionary")
            {
                await RefreshDictionaryStateAsync();
            }
        }

        protected async Task CloseModalAsync()
        {
            IsOpen = false;
            previousIsOpenState = false;
            await IsOpenChanged.InvokeAsync(false);
        }

        protected async Task ToggleTheme(bool dark)
        {
            isDarkMode = dark;
            DbService.Config.IsDarkMode = dark;
            await JSRuntime.InvokeVoidAsync("document.body.setAttribute", "data-bs-theme", dark ? "dark" : "light");
        }

        protected async Task RefreshDictionaryStateAsync()
        {
            dictDirectoryPath = DictionaryService.GetLocalDictionariesFolder();
            activeDictionaryWords = await DictionaryService.SyncAndLoadAllDictionariesAsync();

            loadedDictFiles.Clear();
            if (Directory.Exists(dictDirectoryPath))
            {
                var files = Directory.GetFiles(dictDirectoryPath, "*.txt");
                foreach (var file in files)
                {
                    var lines = await File.ReadAllLinesAsync(file);
                    loadedDictFiles.Add(new DictionaryFileInfo
                    {
                        FileName = Path.GetFileName(file),
                        FullPath = file,
                        WordCount = lines.Length
                    });
                }
            }

            try
            {
                await JSRuntime.InvokeVoidAsync("setGlobalGameDictionary", activeDictionaryWords);
            }
            catch (JSException ex)
            {
                Console.WriteLine($"JS Dict Sync Error: {ex.Message}");
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
                await AddNewWord();
            }
        }

        protected async Task AddNewWord()
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

        protected async Task RemoveWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;

            await DictionaryService.RemoveWordFromAllDictionariesAsync(word);
            await RefreshDictionaryStateAsync();
        }
    }
}
