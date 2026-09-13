using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Components.Layout
{
    public partial class DatabaseSidebarPane : ComponentBase, IDisposable
    {
        [Inject] protected SqliteDataService DbService { get; set; } = default!;
        [Inject] protected NavigationManager NavManager { get; set; } = default!;

        protected List<DbObjectInfo> dbObjects = new();
        protected Dictionary<string, List<Dictionary<string, string>>> tableRecordKeys = new(StringComparer.OrdinalIgnoreCase);

        protected string? selectedTable = null;
        protected string? expandedTable = null;
        protected bool isLoadingTables = true;

        protected string searchFilter = string.Empty;
        protected string typeFilter = "table";

        protected IEnumerable<DbObjectInfo> FilteredObjects => dbObjects
            .Where(o => typeFilter == "all" || o.Type == typeFilter)
            .Where(o => string.IsNullOrWhiteSpace(searchFilter) || o.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));

        protected override async Task OnInitializedAsync()
        {
            DbService.OnDatabasePathChanged += HandleDatabaseChanged;
            await RefreshDatabaseObjectsAsync();
        }

        public async Task RefreshDatabaseObjectsAsync()
        {
            if (!DbService.HasActiveDatabase)
            {
                dbObjects.Clear();
                tableRecordKeys.Clear();
                isLoadingTables = false;
                StateHasChanged();
                return;
            }

            isLoadingTables = true;
            StateHasChanged();

            dbObjects = await DbService.GetTablesAndViewsAsync();
            tableRecordKeys.Clear();

            var firstTable = dbObjects.FirstOrDefault(o => o.Type == "table") ?? dbObjects.FirstOrDefault();
            if (firstTable != null)
            {
                selectedTable = firstTable.Name;
                expandedTable = firstTable.Name;
                await LoadRecordColumnsAsync(firstTable.Name);
            }

            isLoadingTables = false;
            StateHasChanged();
        }

        protected async Task ToggleTableAccordionAsync(string tableName)
        {
            if (expandedTable == tableName)
            {
                expandedTable = null;
            }
            else
            {
                expandedTable = tableName;
                selectedTable = tableName;
                await LoadRecordColumnsAsync(tableName);
            }
        }

        private async Task LoadRecordColumnsAsync(string tableName)
        {
            if (!tableRecordKeys.ContainsKey(tableName))
            {
                var meta = await DbService.GetColumnMetadataAsync(tableName);
                var columns = meta.Where(IncludeColumnsInSidebarPane()).Select(c => c.Key).ToList();

                if (!columns.Any())
                {
                    var tableData = await DbService.GetTableDataAsync(tableName);
                    if (tableData.Columns.Any()) columns.Add(tableData.Columns.First());
                }

                tableRecordKeys[tableName] = await DbService.GetRecordColumnsAsync(tableName, columns);
            }
        }

        private Func<KeyValuePair<string, ColumnMetadata>, bool> IncludeColumnsInSidebarPane()
        {
            return c => c.Value.IsPrimaryKey || string.Compare(c.Value.ColumnName, "name", StringComparison.OrdinalIgnoreCase) == 0;
        }

        protected void NavigateToRecord(string tableName, Dictionary<string, string> keyMap)
        {
            selectedTable = tableName;

            string keyParams = string.Join("&", keyMap.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            NavManager.NavigateTo($"/editor/{Uri.EscapeDataString(tableName)}?{keyParams}");
        }

        protected async Task CloseCurrentDatabaseAsync()
        {
            await DbService.CloseDatabaseAsync();
        }

        private async Task HandleDatabaseChanged(string newPath)
        {
            expandedTable = null;
            selectedTable = null;

            if (string.IsNullOrEmpty(newPath))
            {
                dbObjects.Clear();
                tableRecordKeys.Clear();
                isLoadingTables = false;
                StateHasChanged();
            }
            else
            {
                await RefreshDatabaseObjectsAsync();
            }

            NavManager.NavigateTo("/");
        }

        public void Dispose()
        {
            DbService.OnDatabasePathChanged -= HandleDatabaseChanged;
        }
    }
}
