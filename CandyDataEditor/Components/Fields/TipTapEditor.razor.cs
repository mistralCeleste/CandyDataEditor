using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CandyDataEditor.Components.Fields
{
    public partial class TipTapEditor : ComponentBase, IAsyncDisposable
    {
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected GameDictionaryService DictionaryService { get; set; } = default!;

        public enum ViewMode
        {
            Formatted,
            Markdown,
            Html
        }

        protected string elementId = $"tiptap_{Guid.NewGuid():N}";
        protected ViewMode currentViewMode = ViewMode.Formatted;
        protected string EditorId = $"tiptap_{Guid.NewGuid():N}";
        private DotNetObjectReference<TipTapEditor>? _dotnetRef;
        private bool _isInitialized = false;

        protected bool isIconModalOpen = false;
        protected string searchTerm = string.Empty;

        protected bool isContextMenuVisible = false;
        protected double contextMenuX = 0;
        protected double contextMenuY = 0;
        protected string selectedMisspelledWord = string.Empty;
        protected List<string> suggestions = new();
        private int wordPosFrom = 0;
        private int wordPosTo = 0;

        [Parameter] public string Value { get; set; } = string.Empty;
        [Parameter] public EventCallback<string> ValueChanged { get; set; }
        [Parameter] public string MarkdownValue { get; set; } = string.Empty;
        [Parameter] public EventCallback<string> MarkdownValueChanged { get; set; }
        [Parameter] public EventCallback OnBlur { get; set; }
        [Parameter] public bool IsReadOnly { get; set; } = false;

        private bool _previousReadOnly = false;

        protected async Task SwitchViewMode(ViewMode newMode)
        {
            if (currentViewMode == newMode) return;

            var previousMode = currentViewMode;
            isContextMenuVisible = false;

            if (previousMode == ViewMode.Formatted && _isInitialized)
            {
                var freshMarkdown = await JSRuntime.InvokeAsync<string>("getTipTapMarkdown", EditorId);
                if (!string.IsNullOrEmpty(freshMarkdown))
                {
                    MarkdownValue = freshMarkdown;
                    await MarkdownValueChanged.InvokeAsync(freshMarkdown);
                }

                var freshHtml = await JSRuntime.InvokeAsync<string>("getTipTapHtml", EditorId);
                if (!string.IsNullOrEmpty(freshHtml))
                {
                    Value = freshHtml;
                    await ValueChanged.InvokeAsync(freshHtml);
                }
            }

            currentViewMode = newMode;

            if (newMode == ViewMode.Formatted && _isInitialized)
            {
                if (previousMode == ViewMode.Markdown && !string.IsNullOrWhiteSpace(MarkdownValue))
                {
                    await JSRuntime.InvokeVoidAsync("setTipTapContentFromMarkdown", EditorId, MarkdownValue);
                }
                else if (previousMode == ViewMode.Html && !string.IsNullOrWhiteSpace(Value))
                {
                    await JSRuntime.InvokeVoidAsync("setTipTapContentFromHtml", EditorId, Value);
                }
            }
        }

        protected async Task OnMarkdownRawInput(ChangeEventArgs e)
        {
            string updated = e.Value?.ToString() ?? string.Empty;
            MarkdownValue = updated;
            await MarkdownValueChanged.InvokeAsync(updated);
        }

        protected async Task OnHtmlRawInput(ChangeEventArgs e)
        {
            string updated = e.Value?.ToString() ?? string.Empty;
            Value = updated;
            await ValueChanged.InvokeAsync(updated);
        }

        [JSInvokable]
        public async Task OnEditorBlurred()
        {
            if (OnBlur.HasDelegate)
            {
                await OnBlur.InvokeAsync();
            }
        }

        [JSInvokable]
        public async Task OpenSpellcheckContextMenu(string word, List<string> fastSuggestions, int from, int to, double clientX, double clientY)
        {
            selectedMisspelledWord = word;
            suggestions = fastSuggestions ?? new List<string>();
            wordPosFrom = from;
            wordPosTo = to;
            contextMenuX = clientX;
            contextMenuY = clientY;

            isContextMenuVisible = true;
            StateHasChanged();

            try
            {
                await JSRuntime.InvokeVoidAsync("attachContextMenuDismissListener", _dotnetRef);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error attaching context menu listener: {ex.Message}");
            }
        }

        [JSInvokable]
        public void CloseSpellcheckContextMenu()
        {
            if (isContextMenuVisible)
            {
                isContextMenuVisible = false;
                StateHasChanged();
            }
        }

        protected async Task ReplaceWord(string newWord)
        {
            isContextMenuVisible = false;
            await JSRuntime.InvokeVoidAsync("replaceTipTapRange", EditorId, wordPosFrom, wordPosTo, selectedMisspelledWord, newWord);
        }

        protected async Task AddSelectedWordToDictionary(string wordToAdd)
        {
            isContextMenuVisible = false;

            if (!string.IsNullOrWhiteSpace(wordToAdd))
            {
                await DictionaryService.AddWordToDictionaryFileAsync("custom_user_words.txt", wordToAdd);

                // Sync refreshed set directly across global JS memory
                var updatedDictionary = await DictionaryService.SyncAndLoadAllDictionariesAsync();
                await JSRuntime.InvokeVoidAsync("setGlobalGameDictionary", updatedDictionary);
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            if (_isInitialized && _previousReadOnly != IsReadOnly)
            {
                _previousReadOnly = IsReadOnly;
                await JSRuntime.InvokeVoidAsync("setTipTapEditable", EditorId, !IsReadOnly);
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _dotnetRef = DotNetObjectReference.Create(this);
                _previousReadOnly = IsReadOnly;

                try
                {
                    await JSRuntime.InvokeVoidAsync("initTipTap", EditorId, MarkdownValue, _dotnetRef, IsReadOnly);
                    _isInitialized = true;
                }
                catch (JSException ex)
                {
                    Console.WriteLine($"TipTap Init Error: {ex.Message}");
                }
            }
        }

        protected async Task OpenIconModal()
        {
            var selectedText = await JSRuntime.InvokeAsync<string>("getTipTapSelectedText", EditorId);
            searchTerm = selectedText ?? string.Empty;
            isIconModalOpen = true;
        }

        protected void CloseIconModal() => isIconModalOpen = false;

        protected async Task SelectIcon(string iconTag)
        {
            isIconModalOpen = false;
            await ExecCommand("insertIcon", iconTag);
        }

        protected async Task ExecCommand(string commandName, string? value = null)
        {
            await JSRuntime.InvokeVoidAsync("execTipTapCommand", EditorId, commandName, value);
        }

        [JSInvokable]
        public async Task OnContentChanged(string html, string markdown)
        {
            Value = html;
            await ValueChanged.InvokeAsync(html);

            MarkdownValue = markdown;
            await MarkdownValueChanged.InvokeAsync(markdown);
        }

        [Inject] protected SqliteEditorConfig Config { get; set; } = default!;

        private static readonly List<string> DefaultIcons = new() { };

        protected List<string> FilteredIcons => string.IsNullOrWhiteSpace(searchTerm)
            ? (Config?.DetectedLigatures?.Count > 0 ? Config.DetectedLigatures : DefaultIcons)
            : (Config?.DetectedLigatures?.Count > 0 ? Config.DetectedLigatures : DefaultIcons)
                .Where(i => i.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        public async ValueTask DisposeAsync()
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("destroyTipTap", EditorId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing TipTap editor JS interop: {ex}");
            }

            _dotnetRef?.Dispose();
        }
    }
}
