using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Components.MessageBoxes
{
    public partial class UnsavedChangesModal : ComponentBase
    {
        [Parameter] public bool IsOpen { get; set; }
        [Parameter] public EventCallback<bool> IsOpenChanged { get; set; }
        [Parameter] public EventCallback OnDiscard { get; set; }
        [Parameter] public EventCallback OnSave { get; set; }

        protected async Task DiscardAsync()
        {
            if (OnDiscard.HasDelegate)
            {
                await OnDiscard.InvokeAsync();
            }
            await CloseModalAsync();
        }

        protected async Task SaveAsync()
        {
            if (OnSave.HasDelegate)
            {
                await OnSave.InvokeAsync();
            }
            await CloseModalAsync();
        }

        private async Task CloseModalAsync()
        {
            IsOpen = false;
            await IsOpenChanged.InvokeAsync(false);
        }
    }
}