using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;
using System.Timers;

namespace CandyDataEditor.Components.Layout
{
    public partial class DatabaseSidebarPane : ComponentBase, IDisposable
    {
        [Inject] protected SqliteDataService DbService { get; set; } = default!;
        [Inject] protected NavigationManager NavManager { get; set; } = default!;

        protected bool isCollapsed = false;
        protected bool isTablesSectionExpanded = true;
        protected bool isSearchResultsExpanded = true;

        protected List<DbObjectInfo> dbObjects = new();
        protected Dictionary<string, List<Dictionary<string, string>>> tableRecordKeys = new(StringComparer.OrdinalIgnoreCase);

        protected string? selectedTable = null;
        protected string? expandedTable = null;
        protected bool isLoadingTables = true;

        protected string searchFilter = string.Empty;
        protected string typeFilter = "table";

        protected List<SearchResultItem> searchResults = new();
        protected bool isSearchingData = false;
        protected float debounceDelayInMilliseconds = 350f;
        private System.Timers.Timer? _debounceTimer;

        protected IEnumerable<DbObjectInfo> FilteredObjects => dbObjects
            .Where(o => typeFilter == "all" || o.Type == typeFilter)
            .Where(o => string.IsNullOrWhiteSpace(searchFilter) || o.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));

        protected override async Task OnInitializedAsync()
        {
            DbService.OnDatabasePathChanged += HandleDatabaseChanged;
            DbService.OnDataChanged += HandleDataChangedAsync;

            _debounceTimer = new System.Timers.Timer(debounceDelayInMilliseconds) { AutoReset = false };
            _debounceTimer.Elapsed += OnDebounceTimerElapsedAsync;

            await RefreshDatabaseObjectsAsync();
        }

        protected void SetTypeFilter(string filter)
        {
            typeFilter = filter;
            TriggerDebouncedDataSearch();
        }

        protected void OnSearchInputChanged(ChangeEventArgs e)
        {
            searchFilter = e.Value?.ToString() ?? string.Empty;
            TriggerDebouncedDataSearch();
        }

        private void TriggerDebouncedDataSearch()
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Start();
        }

        private async void OnDebounceTimerElapsedAsync(object? sender, ElapsedEventArgs e)
        {
            await InvokeAsync(async () =>
            {
                if (string.IsNullOrWhiteSpace(searchFilter))
                {
                    searchResults.Clear();
                    isSearchingData = false;
                    StateHasChanged();
                    return;
                }

                isSearchingData = true;
                StateHasChanged();

                var activeTables = dbObjects
                    .Where(o => typeFilter == "all" || o.Type == typeFilter)
                    .Select(o => o.Name)
                    .ToList();

                searchResults = await DbService.SearchDataAcrossObjectsAsync(activeTables, searchFilter);
                isSearchingData = false;
                StateHasChanged();
            });
        }

        protected void NavigateToSearchMatch(SearchResultItem match)
        {
            selectedTable = match.TableName;

            var queryParams = match.PrimaryKeys.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}").ToList();
            queryParams.Add($"FocusColumn={Uri.EscapeDataString(match.MatchingColumn)}");
            queryParams.Add($"SearchTerm={Uri.EscapeDataString(match.MatchTerm)}");

            string keyParams = string.Join("&", queryParams);
            NavManager.NavigateTo($"/editor/{Uri.EscapeDataString(match.TableName)}?{keyParams}");
        }

        public async Task RefreshDatabaseObjectsAsync()
        {
            if (!DbService.HasActiveDatabase)
            {
                dbObjects.Clear();
                tableRecordKeys.Clear();
                searchResults.Clear();
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

        protected void ToggleTablesSection()
        {
            isTablesSectionExpanded = !isTablesSectionExpanded;
        }

        protected void ToggleSearchResultsSection()
        {
            isSearchResultsExpanded = !isSearchResultsExpanded;
        }

        protected void ToggleCollapse() => isCollapsed = !isCollapsed;

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

        protected async Task CloseCurrentDatabaseAsync() => await DbService.CloseDatabaseAsync();

        private async Task HandleDatabaseChanged(string newPath)
        {
            expandedTable = null;
            selectedTable = null;
            searchResults.Clear();

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

        private async Task HandleDataChangedAsync()
        {
            string? currentExpanded = expandedTable;

            dbObjects = await DbService.GetTablesAndViewsAsync();
            tableRecordKeys.Clear();

            if (!string.IsNullOrEmpty(currentExpanded))
            {
                await LoadRecordColumnsAsync(currentExpanded);
            }

            await InvokeAsync(StateHasChanged);
        }

        protected void NavigateToTableGrid(string tableName)
        {
            selectedTable = tableName;
            NavManager.NavigateTo($"/table-editor/{Uri.EscapeDataString(tableName)}");
        }

        public void Dispose()
        {
            DbService.OnDatabasePathChanged -= HandleDatabaseChanged;
            DbService.OnDataChanged -= HandleDataChangedAsync;
            if (_debounceTimer != null)
            {
                _debounceTimer.Elapsed -= OnDebounceTimerElapsedAsync;
                _debounceTimer.Dispose();
            }
        }
    }
}
