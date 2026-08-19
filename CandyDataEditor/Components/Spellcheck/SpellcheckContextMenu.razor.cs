// Components/SpellcheckContextMenu.razor.cs
using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Components.Spellcheck
{
    public partial class SpellcheckContextMenu : ComponentBase
    {
        [Parameter] public bool IsVisible { get; set; }
        [Parameter] public EventCallback<bool> IsVisibleChanged { get; set; }

        [Parameter] public double X { get; set; }
        [Parameter] public double Y { get; set; }
        [Parameter] public string Word { get; set; } = string.Empty;
        [Parameter] public List<string> Suggestions { get; set; } = new();

        [Parameter] public EventCallback<string> OnReplaceWord { get; set; }
        [Parameter] public EventCallback<string> OnAddToDictionary { get; set; }

        protected async Task ReplaceWordAsync(string suggestion)
        {
            if (OnReplaceWord.HasDelegate)
            {
                await OnReplaceWord.InvokeAsync(suggestion);
            }
            await CloseMenuAsync();
        }

        protected async Task AddToDictionaryAsync()
        {
            if (OnAddToDictionary.HasDelegate)
            {
                await OnAddToDictionary.InvokeAsync(Word);
            }
            await CloseMenuAsync();
        }

        private async Task CloseMenuAsync()
        {
            IsVisible = false;
            await IsVisibleChanged.InvokeAsync(false);
        }
    }
}
