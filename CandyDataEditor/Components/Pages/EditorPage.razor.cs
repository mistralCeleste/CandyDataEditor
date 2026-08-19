using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Web;

namespace CandyDataEditor.Pages
{
    public partial class EditorPage : ComponentBase
    {
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] protected SqliteDataService DbService { get; set; } = default!;
        [Inject] protected NavigationManager NavManager { get; set; } = default!;

        [Parameter] public string? TableName { get; set; }

        [SupplyParameterFromQuery]
        public string? Id { get; set; } // Matches single PKs like ?Id=10

        protected List<DbObjectInfo> dbObjects = new();
        protected string selectedTable = string.Empty;
        protected string expandedTable = string.Empty;
        protected TableDataResult? tableData;
        protected Dictionary<string, ColumnMetadata> columnMetadata = new(StringComparer.OrdinalIgnoreCase);
        protected Dictionary<string, List<Dictionary<string, string>>> tableRecordKeys = new(StringComparer.OrdinalIgnoreCase);

        protected string dbPath = string.Empty;
        protected bool isLoadingTables = true;
        protected bool isLoadingData = false;

        // Filters
        protected string searchFilter = string.Empty;
        protected string typeFilter = "all"; // 'all', 'table', 'view'

        // Record Editing State
        protected Dictionary<string, string>? editingRow = null;
        protected Dictionary<string, string> originalRowSnapshot = new(StringComparer.OrdinalIgnoreCase);
        protected Dictionary<string, string> originalKeys = new(StringComparer.OrdinalIgnoreCase);

        // Auto-Save & Change Tracking
        protected bool autoSaveEnabled = false;
        protected bool showUnsavedModal = false;
        private Dictionary<string, string>? pendingNavigationTargetKey = null;
        private string? pendingNavigationTable = null;

        // Error Messages
        protected string? saveErrorMessage = null;
        protected Dictionary<string, string> fieldErrorMessages = new(StringComparer.OrdinalIgnoreCase);

        protected IEnumerable<DbObjectInfo> FilteredObjects => dbObjects
            .Where(o => typeFilter == "all" || o.Type == typeFilter)
            .Where(o => string.IsNullOrWhiteSpace(searchFilter) || o.Name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));

        protected override async Task OnInitializedAsync()
        {
            dbPath = DbService.GetDatabasePath();
            dbObjects = await DbService.GetTablesAndViewsAsync();
            isLoadingTables = false;

            if (dbObjects.Any())
            {
                await SelectTable(dbObjects.First().Name);
            }
        }

        protected override async Task OnParametersSetAsync()
        {
            if (string.IsNullOrEmpty(TableName)) return;

            if (selectedTable != TableName)
            {
                await SelectTable(TableName);
            }

            var uri = NavManager.ToAbsoluteUri(NavManager.Uri);
            var queryParams = HttpUtility.ParseQueryString(uri.Query);

            if (queryParams.Count > 0)
            {
                var targetKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (string? key in queryParams.AllKeys)
                {
                    if (!string.IsNullOrEmpty(key) && queryParams[key] != null)
                    {
                        targetKeys[key] = queryParams[key]!;
                    }
                }

                if (targetKeys.Any())
                {
                    await ExecuteNavigationForTable(TableName, targetKeys);
                }
            }
            else
            {
                CloseRecordEditor();
            }
        }

        protected async Task SelectTable(string tableName)
        {
            editingRow = null;
            originalRowSnapshot.Clear();
            originalKeys.Clear();
            saveErrorMessage = null;
            fieldErrorMessages.Clear();

            selectedTable = tableName;
            isLoadingData = true;

            tableData = await DbService.GetTableDataAsync(tableName);
            columnMetadata = await DbService.GetColumnMetadataAsync(tableName);

            var pkCols = columnMetadata.Where(c => c.Value.IsPrimaryKey).Select(c => c.Key).ToList();
            if (!pkCols.Any() && tableData.Columns.Any()) pkCols.Add(tableData.Columns.First());

            tableRecordKeys[tableName] = await DbService.GetRecordKeysAsync(tableName, pkCols);

            isLoadingData = false;
        }

        protected string GetFieldValue(string columnName)
        {
            if (editingRow != null && editingRow.TryGetValue(columnName, out var val))
            {
                return val ?? string.Empty;
            }
            return string.Empty;
        }

        protected void SetFieldValue(string columnName, string value)
        {
            if (editingRow != null)
            {
                editingRow[columnName] = value;
            }
        }

        protected async Task ToggleTableAccordion(string tableName)
        {
            if (expandedTable == tableName)
            {
                expandedTable = string.Empty;
            }
            else
            {
                expandedTable = tableName;
                if (selectedTable != tableName)
                {
                    await SelectTable(tableName);
                }
            }
        }

        protected void OpenRecordEditor(Dictionary<string, string> row)
        {
            saveErrorMessage = null;
            fieldErrorMessages.Clear();

            editingRow = new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase);
            originalRowSnapshot = new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase);

            originalKeys.Clear();
            foreach (var kvp in columnMetadata)
            {
                if (kvp.Value.IsPrimaryKey && row.ContainsKey(kvp.Key))
                {
                    originalKeys[kvp.Key] = row[kvp.Key];
                }
            }
        }

        protected void CloseRecordEditor()
        {
            editingRow = null;
            originalRowSnapshot.Clear();
            originalKeys.Clear();
            saveErrorMessage = null;
            fieldErrorMessages.Clear();
        }

        protected bool HasUnsavedChanges()
        {
            if (editingRow == null) return false;
            foreach (var kvp in editingRow)
            {
                string orig = originalRowSnapshot.TryGetValue(kvp.Key, out var oVal) ? NormalizeValue(oVal) : "";
                string curr = NormalizeValue(kvp.Value);

                if (!string.Equals(curr, orig, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        protected string NormalizeValue(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return "";
            string trimmed = val.Trim();
            if (trimmed == "<p></p>" || trimmed == "<p><br></p>" || trimmed == "<br>") return "";
            return trimmed;
        }

        protected async Task OnFieldFocusLost()
        {
            if (editingRow == null) return;

            var recalculatedGenFields = await DbService.RecalculateGeneratedFieldsAsync(selectedTable, editingRow);

            foreach (var kvp in recalculatedGenFields)
            {
                editingRow[kvp.Key] = kvp.Value;
            }

            if (autoSaveEnabled && HasUnsavedChanges())
            {
                await SaveCurrentRecord();
            }

            StateHasChanged();
        }

        protected async Task RequestRecordNavigationForTable(string targetTable, Dictionary<string, string>? targetKeyMap)
        {
            if (!autoSaveEnabled && HasUnsavedChanges())
            {
                pendingNavigationTable = targetTable;
                pendingNavigationTargetKey = targetKeyMap;
                showUnsavedModal = true;
                return;
            }

            if (autoSaveEnabled && HasUnsavedChanges())
            {
                await SaveCurrentRecord();
            }

            await ExecuteNavigationForTable(targetTable, targetKeyMap);
        }

        protected async Task RequestRecordNavigation(Dictionary<string, string>? targetKeyMap)
        {
            await RequestRecordNavigationForTable(selectedTable, targetKeyMap);
        }

        protected async Task ExecuteNavigationForTable(string targetTable, Dictionary<string, string>? targetKeyMap)
        {
            if (targetKeyMap == null)
            {
                CloseRecordEditor();
                return;
            }

            if (selectedTable != targetTable || tableData == null)
            {
                await SelectTable(targetTable);
            }

            var record = await DbService.GetRecordByKeysAsync(targetTable, targetKeyMap);

            if (record != null)
            {
                saveErrorMessage = null;
                fieldErrorMessages.Clear();

                originalKeys = new Dictionary<string, string>(targetKeyMap, StringComparer.OrdinalIgnoreCase);
                editingRow = new Dictionary<string, string>(record, StringComparer.OrdinalIgnoreCase);
                originalRowSnapshot = new Dictionary<string, string>(record, StringComparer.OrdinalIgnoreCase);

                StateHasChanged();
            }
        }

        protected async Task SaveAndProceed()
        {
            showUnsavedModal = false;
            bool saved = await SaveCurrentRecord();
            if (saved)
            {
                string targetTable = pendingNavigationTable ?? selectedTable;
                var targetKey = pendingNavigationTargetKey;
                pendingNavigationTable = null;
                pendingNavigationTargetKey = null;

                await ExecuteNavigationForTable(targetTable, targetKey);
            }
        }

        protected async Task RevertAndProceed()
        {
            showUnsavedModal = false;
            string targetTable = pendingNavigationTable ?? selectedTable;
            var targetKey = pendingNavigationTargetKey;
            pendingNavigationTable = null;
            pendingNavigationTargetKey = null;

            await ExecuteNavigationForTable(targetTable, targetKey);
        }

        protected async Task<bool> SaveCurrentRecord()
        {
            if (editingRow == null) return false;

            saveErrorMessage = null;
            fieldErrorMessages.Clear();

            var writableValues = editingRow
                .Where(kvp =>
                {
                    if (columnMetadata.TryGetValue(kvp.Key, out var meta))
                    {
                        return !meta.IsGenerated && !meta.IsReadOnly;
                    }
                    return !kvp.Key.Equals("Full Id", StringComparison.OrdinalIgnoreCase);
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            string? error = null;

            if (originalKeys == null || originalKeys.Count == 0)
            {
                error = await DbService.InsertRecordAsync(selectedTable, writableValues);
            }
            else
            {
                error = await DbService.SaveRecordAsync(selectedTable, originalKeys, writableValues);
            }

            if (error != null)
            {
                saveErrorMessage = error;
                if (error.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
                    error.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var col in columnMetadata.Where(c => c.Value.IsPrimaryKey).Select(c => c.Key))
                    {
                        fieldErrorMessages[col] = "Key conflict! This key combination already exists.";
                    }
                }
                return false;
            }

            originalRowSnapshot = new Dictionary<string, string>(editingRow, StringComparer.OrdinalIgnoreCase);
            originalKeys.Clear();
            foreach (var kvp in columnMetadata.Where(c => c.Value.IsPrimaryKey))
            {
                if (editingRow.ContainsKey(kvp.Key))
                    originalKeys[kvp.Key] = editingRow[kvp.Key];
            }

            var pkCols = columnMetadata.Where(c => c.Value.IsPrimaryKey).Select(c => c.Key).ToList();
            if (!pkCols.Any() && tableData != null && tableData.Columns.Any())
                pkCols.Add(tableData.Columns.First());

            tableRecordKeys[selectedTable] = await DbService.GetRecordKeysAsync(selectedTable, pkCols);
            return true;
        }

        protected async Task CreateNewRecord()
        {
            var newRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in tableData!.Columns)
            {
                newRow[col] = col.Equals("Version", StringComparison.OrdinalIgnoreCase) ? "1" : "";
            }

            OpenRecordEditor(newRow);
            originalKeys.Clear();
        }

        protected async Task CloneCurrentRecord()
        {
            if (editingRow == null) return;

            var clonedRow = new Dictionary<string, string>(editingRow, StringComparer.OrdinalIgnoreCase);

            foreach (var pkCol in columnMetadata.Where(c => c.Value.IsPrimaryKey).Select(c => c.Key))
            {
                if (clonedRow.ContainsKey(pkCol))
                {
                    clonedRow[pkCol] = clonedRow[pkCol] + "_COPY";
                }
            }

            var keysToRemove = clonedRow.Keys
                .Where(k => (columnMetadata.TryGetValue(k, out var m) && m.IsGenerated) || k.Equals("Full Id", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                clonedRow.Remove(key);
            }

            OpenRecordEditor(clonedRow);
            originalKeys.Clear();
        }

        protected async Task DeleteCurrentRecord()
        {
            if (editingRow == null) return;

            string? err = await DbService.DeleteRecordAsync(selectedTable, originalKeys);
            if (err != null)
            {
                saveErrorMessage = err;
                return;
            }

            editingRow = null;
            await SelectTable(selectedTable);
        }

        protected bool HasPreviousRecord => GetCurrentRecordIndex() > 0;
        protected bool HasNextRecord => tableRecordKeys.ContainsKey(selectedTable) && GetCurrentRecordIndex() < tableRecordKeys[selectedTable].Count - 1;

        protected int GetCurrentRecordIndex()
        {
            if (!tableRecordKeys.ContainsKey(selectedTable)) return -1;
            var list = tableRecordKeys[selectedTable];
            for (int i = 0; i < list.Count; i++)
            {
                if (IsMatchingKeys(list[i], originalKeys)) return i;
            }
            return -1;
        }

        protected async Task NavigateToPreviousRecord()
        {
            int idx = GetCurrentRecordIndex();
            if (idx > 0)
            {
                await RequestRecordNavigation(tableRecordKeys[selectedTable][idx - 1]);
            }
        }

        protected async Task NavigateToNextRecord()
        {
            int idx = GetCurrentRecordIndex();
            if (idx >= 0 && idx < tableRecordKeys[selectedTable].Count - 1)
            {
                await RequestRecordNavigation(tableRecordKeys[selectedTable][idx + 1]);
            }
        }

        protected async Task OnPkJumpSelected(ChangeEventArgs e)
        {
            string? val = e.Value?.ToString();
            if (string.IsNullOrEmpty(val) || !tableRecordKeys.ContainsKey(selectedTable)) return;

            var target = tableRecordKeys[selectedTable].FirstOrDefault(km => FormatKeyMapLabel(km) == val);
            if (target != null)
            {
                await RequestRecordNavigation(target);
            }
        }

        protected string FormatKeyMapLabel(Dictionary<string, string> keyMap)
        {
            return string.Join(" | ", keyMap.Values);
        }

        protected bool IsMatchingKeys(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var val) || val != kvp.Value) return false;
            }
            return true;
        }

        protected async Task OnDatabaseChanged(string newPath)
        {
            editingRow = null;
            originalKeys = new Dictionary<string, string>();
            pendingNavigationTargetKey = null;
            pendingNavigationTable = null;

            expandedTable = string.Empty;
            selectedTable = string.Empty;
            tableRecordKeys.Clear();
            columnMetadata.Clear();

            isLoadingTables = true;
            StateHasChanged();

            try
            {
                dbPath = newPath;
                dbObjects = await DbService.GetTablesAndViewsAsync();

                var firstTable = dbObjects.FirstOrDefault(o => o.Type == "table") ?? dbObjects.FirstOrDefault();
                if (firstTable != null)
                {
                    selectedTable = firstTable.Name;
                    expandedTable = firstTable.Name;
                    await SelectTable(firstTable.Name);

                    if (tableRecordKeys.ContainsKey(firstTable.Name) && tableRecordKeys[firstTable.Name].Any())
                    {
                        var firstRecordKey = tableRecordKeys[firstTable.Name].First();
                        await ExecuteNavigationForTable(firstTable.Name, firstRecordKey);
                    }
                }
            }
            finally
            {
                isLoadingTables = false;
                StateHasChanged();
            }
        }
    }
}
