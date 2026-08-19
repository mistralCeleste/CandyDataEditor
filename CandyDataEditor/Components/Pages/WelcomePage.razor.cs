// Pages/WelcomePage.razor.cs
using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Pages
{
    public partial class WelcomePage : ComponentBase, IDisposable
    {
        [Inject] protected SqliteDataService DbService { get; set; } = default!;
        [Inject] protected GameDictionaryService DictionaryService { get; set; } = default!;
        [Inject] protected NavigationManager NavManager { get; set; } = default!;

        protected bool IsDictionaryLoaded { get; set; } = false;
        protected int WordCount { get; set; } = 0;

        protected override async Task OnInitializedAsync()
        {
            DictionaryService.OnDictionaryLoaded += HandleDictionaryLoaded;
            DbService.OnDatabasePathChanged += HandleDatabasePathChanged;

            if (DictionaryService.WordCount > 0)
            {
                WordCount = DictionaryService.WordCount;
                IsDictionaryLoaded = true;
            }

            await Task.CompletedTask;
        }

        private void HandleDictionaryLoaded(List<string> wordList)
        {
            InvokeAsync(() =>
            {
                WordCount = DictionaryService.WordCount;
                IsDictionaryLoaded = WordCount > 0;
                StateHasChanged();
            });
        }

        protected async Task OpenDatabaseAsync(string path)
        {
            if (File.Exists(path))
            {
                await DbService.SetDatabasePathAsync(path);
            }
            else
            {
                DbService.Config.RemoveRecentDatabase(path);
                StateHasChanged();
            }
        }

        private Task HandleDatabasePathChanged(string newPath)
        {
            return InvokeAsync(StateHasChanged);
        }

        public void Dispose()
        {
            DictionaryService.OnDictionaryLoaded -= HandleDictionaryLoaded;
            DbService.OnDatabasePathChanged -= HandleDatabasePathChanged;
        }

        public class RecentRecordItem
        {
            public string TableId { get; set; } = string.Empty;
            public string TableName { get; set; } = string.Empty;
            public string RecordId { get; set; } = string.Empty;
            public string RecordName { get; set; } = string.Empty;
            public DateTime LastEdited { get; set; }
        }
    }
}
