// Components/SqliteSettingsPane.razor.cs
using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Components.Settings
{
    public partial class SqliteSettingsPane: ComponentBase
    {
        [Inject] protected SqliteDataService DbService { get; set; } = default!;
    }
}
