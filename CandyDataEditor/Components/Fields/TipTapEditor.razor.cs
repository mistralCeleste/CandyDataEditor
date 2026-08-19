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

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _dotnetRef = DotNetObjectReference.Create(this);

                try
                {
                    await JSRuntime.InvokeVoidAsync("initTipTap", EditorId, MarkdownValue, _dotnetRef);
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

        protected List<string> FilteredIcons => string.IsNullOrWhiteSpace(searchTerm)
            ? AllIcons
            : AllIcons.Where(i => i.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        protected static readonly List<string> AllIcons = new()
        {
            "action", "actor", "aegis", "allfoes", "anvil", "any", "aquarius", "aries", "armor", "arrowhead",
            "banish", "book", "booklet", "boss", "brute", "burn", "buycard", "cancer", "capricorn",
            "card10clubs", "card10diamonds", "card10hearts", "card10spades",
            "card2clubs", "card2diamonds", "card2hearts", "card2spades",
            "card3clubs", "card3diamonds", "card3hearts", "card3spades",
            "card4clubs", "card4diamonds", "card4hearts", "card4spades",
            "card5clubs", "card5diamonds", "card5hearts", "card5spades",
            "card6clubs", "card6diamonds", "card6hearts", "card6spades",
            "card7clubs", "card7diamonds", "card7hearts", "card7spades",
            "card8clubs", "card8diamonds", "card8hearts", "card8spades",
            "card9clubs", "card9diamonds", "card9hearts", "card9spades",
            "cardaceclubs", "cardacediamonds", "cardacehearts", "cardacespades",
            "cardjackclubs", "cardjackdiamonds", "cardjackhearts", "cardjackspades", "cardjoker",
            "cardkingclubs", "cardkingdiamonds", "cardkinghearts", "cardkingspades",
            "cardqueenclubs", "cardqueendiamonds", "cardqueenhearts", "cardqueenspades",
            "cardboardboxclosed", "cardboardbox", "coin", "combat", "concat", "critical", "cycle",
            "d10", "d12", "d201", "d2020", "d4", "d61", "d62", "d63", "d64", "d65", "d66", "d8",
            "damagenull", "damagex", "damage", "day", "defense", "dicefire", "diceshield", "dicetarget", "dice", "diff",
            "directionaldamagenull", "directionaldamagex", "directionaldamage", "discard", "draw", "eatery", "elite",
            "ethereal", "event", "exchange", "flying", "foe", "foes", "fudgeblank", "fudgeminus", "fudgeplus", "gain",
            "gemini", "goto", "health", "hero", "heroes", "idcard", "interruption", "item", "keycard", "key", "leo",
            "libra", "location", "lock", "lose", "marker", "melee", "might", "minus", "mob", "moondial", "move", "nextchoice",
            "no", "npc", "null", "onehanded", "openbook", "overlord", "passive", "pathfinder", "pickup", "pisces", "place",
            "play", "plus", "pull", "push", "quest", "random", "range", "ranged", "reaction", "refreshmarker", "refresh",
            "remove", "repeat2", "repeat3", "repeatcritical", "repeatdice", "repeatfoe", "repeatmarker",
            "repeatmight", "repeatthreat", "repeatvalor", "repeatwarning", "repeatweapon", "repeatwisdom", "repeat",
            "reroll", "retrieve", "return", "room", "sage", "sagittarius", "scorpio", "search", "sellcard", "slota",
            "slotaegislocked", "slotaegis", "slotany", "slotb", "slotblank", "slotc", "slotd", "slote",
            "slotlocked", "slotmarkerlocked", "slotmarker", "slotmightlocked", "slotmight", "slotminus",
            "slotplus", "slotrefresh", "slotthreat", "slotvalorlocked", "slotvalor", "slotwisdomlocked",
            "slotwisdom", "slotx", "snap", "special", "stack", "sundial", "swap", "tableau", "target", "taurus",
            "threat", "tierbronze", "tiergold", "tierplatinum", "tiersilver", "tierstarter", "trade", "trophy",
            "twohanded", "valor", "vanguard", "virgo", "warblade", "warning", "weapon", "wheel", "wing", "wisdom", "x", "yes"
        };

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