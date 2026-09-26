using CandyDataEditor.Services;
using Microsoft.AspNetCore.Components;

namespace CandyDataEditor.Pages
{
    public partial class TableGridEditorPage : ComponentBase
    {
        [Inject] protected SqliteDataService DbService { get; set; } = default!;

        [Parameter] public string TableName { get; set; } = string.Empty;

        protected List<string> columns = new();
        protected Dictionary<string, ColumnMetadata> columnMetadata = new(StringComparer.OrdinalIgnoreCase);
        protected List<GridRowModel> gridRows = new();
        protected int selectedRowIndex = -1;

        protected bool isLoading = true;
        protected bool isSaving = false;
        protected string? statusMessage;

        protected bool showMessageBox = false;
        protected string messageBoxTitle = "Error";
        protected string messageBoxMessage = string.Empty;

        public class GridRowModel
        {
            public Guid RowId { get; } = Guid.NewGuid(); // Unique key for Blazor DOM diffing
            public bool IsNewRow { get; set; } = false;
            public Dictionary<string, string> OriginalKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> OriginalValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> CurrentValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> FieldErrors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

            public bool IsDirty => IsNewRow || CurrentValues.Any(kvp => !string.Equals(kvp.Value, OriginalValues.GetValueOrDefault(kvp.Key, ""), StringComparison.Ordinal));
        }

        protected bool HasValidationErrors => gridRows.Any(r => r.FieldErrors.Any());

        protected override async Task OnParametersSetAsync()
        {
            await ReloadDataAsync();
        }

        protected async Task ReloadDataAsync()
        {
            if (string.IsNullOrEmpty(TableName)) return;

            isLoading = true;
            statusMessage = null;
            selectedRowIndex = -1;
            StateHasChanged();

            columnMetadata = await DbService.GetColumnMetadataAsync(TableName);
            var tableData = await DbService.GetTableDataAsync(TableName);

            columns = tableData.Columns;
            gridRows.Clear();

            foreach (var row in tableData.Rows)
            {
                var keys = ExtractPrimaryKeyMap(row);

                gridRows.Add(new GridRowModel
                {
                    IsNewRow = false,
                    OriginalKeys = keys,
                    OriginalValues = new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase),
                    CurrentValues = new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase)
                });
            }

            ValidateGrid();
            isLoading = false;
            StateHasChanged();
        }

        private Dictionary<string, string> ExtractPrimaryKeyMap(Dictionary<string, string> rowValues)
        {
            var keys = columnMetadata
                .Where(c => c.Value.IsPrimaryKey)
                .ToDictionary(c => c.Key, c => rowValues.GetValueOrDefault(c.Key, ""), StringComparer.OrdinalIgnoreCase);

            if (!keys.Any() && columns.Any())
            {
                keys[columns.First()] = rowValues.GetValueOrDefault(columns.First(), "");
            }

            return keys;
        }

        protected void ToggleRowSelection(int index)
        {
            if (selectedRowIndex == index)
            {
                selectedRowIndex = -1; // Unselect if clicked again
            }
            else
            {
                selectedRowIndex = index; // Select new row
            }
        }

        protected void OnCellValueChanged(GridRowModel row, string colName, string val)
        {
            row.CurrentValues[colName] = val;
            ValidateGrid();
        }

        /* FIX 3: Creates a strictly EMPTY record */
        protected void AddNewRow()
        {
            var newRowValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in columns)
            {
                newRowValues[col] = col.Equals("Version", StringComparison.OrdinalIgnoreCase) ? "1" : "";
            }

            var newGridRow = new GridRowModel
            {
                IsNewRow = true,
                OriginalKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                OriginalValues = new Dictionary<string, string>(newRowValues, StringComparer.OrdinalIgnoreCase),
                CurrentValues = new Dictionary<string, string>(newRowValues, StringComparer.OrdinalIgnoreCase)
            };

            gridRows.Insert(0, newGridRow);
            selectedRowIndex = 0;
            ValidateGrid();
        }

        protected void CloneSelectedRow()
        {
            if (selectedRowIndex < 0 || selectedRowIndex >= gridRows.Count) return;

            var targetRow = gridRows[selectedRowIndex];
            var clonedValues = new Dictionary<string, string>(targetRow.CurrentValues, StringComparer.OrdinalIgnoreCase);

            foreach (var pkCol in columnMetadata.Where(c => c.Value.IsPrimaryKey).Select(c => c.Key))
            {
                if (clonedValues.ContainsKey(pkCol) && !string.IsNullOrWhiteSpace(clonedValues[pkCol]))
                {
                    clonedValues[pkCol] = clonedValues[pkCol] + "_COPY";
                }
            }

            var clonedGridRow = new GridRowModel
            {
                IsNewRow = true,
                OriginalKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                OriginalValues = new Dictionary<string, string>(clonedValues, StringComparer.OrdinalIgnoreCase),
                CurrentValues = new Dictionary<string, string>(clonedValues, StringComparer.OrdinalIgnoreCase)
            };

            gridRows.Insert(selectedRowIndex + 1, clonedGridRow);
            selectedRowIndex = selectedRowIndex + 1;
            ValidateGrid();
        }

        protected void RemoveRow(int index)
        {
            if (index >= 0 && index < gridRows.Count)
            {
                gridRows.RemoveAt(index);
                if (selectedRowIndex == index) selectedRowIndex = -1;
                else if (selectedRowIndex > index) selectedRowIndex--;
                ValidateGrid();
            }
        }

        protected void ValidateGrid()
        {
            var pkCols = columnMetadata.Where(c => c.Value.IsPrimaryKey).Select(c => c.Key).ToList();
            if (!pkCols.Any() && columns.Any()) pkCols.Add(columns.First());

            foreach (var row in gridRows)
            {
                row.FieldErrors.Clear();
            }

            for (int i = 0; i < gridRows.Count; i++)
            {
                var row = gridRows[i];
                foreach (var pk in pkCols)
                {
                    string rawVal = row.CurrentValues.GetValueOrDefault(pk, "");
                    string val = CleanTextValue(rawVal);

                    if (string.IsNullOrWhiteSpace(val))
                    {
                        row.FieldErrors[pk] = "Primary Key cannot be empty.";
                    }
                }
            }

            var keyMapTracker = new Dictionary<string, int>();

            for (int i = 0; i < gridRows.Count; i++)
            {
                var row = gridRows[i];
                var pkValues = pkCols.Select(pk => CleanTextValue(row.CurrentValues.GetValueOrDefault(pk, ""))).ToList();

                if (pkValues.Any(string.IsNullOrWhiteSpace)) continue;

                string compositeKey = string.Join("|||", pkValues);

                if (keyMapTracker.TryGetValue(compositeKey, out int existingRowNumber))
                {
                    foreach (var pk in pkCols)
                    {
                        row.FieldErrors[pk] = $"Conflict: Duplicate Primary Key matches Row #{existingRowNumber}.";
                    }

                    var originalRow = gridRows[existingRowNumber - 1];
                    foreach (var pk in pkCols)
                    {
                        originalRow.FieldErrors[pk] = $"Conflict: Duplicate Primary Key matches Row #{i + 1}.";
                    }
                }
                else
                {
                    keyMapTracker[compositeKey] = i + 1;
                }
            }
        }

        private string CleanTextValue(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return "";
            string trimmed = val.Trim();
            if (trimmed == "<p></p>" || trimmed == "<p><br></p>" || trimmed == "<br>" || trimmed == "\\n") return "";
            return trimmed;
        }

        protected async Task SaveAllChangesAsync()
        {
            ValidateGrid();
            if (HasValidationErrors)
            {
                ShowErrorDialog("Validation Errors", "Please fix the highlighted Primary Key conflicts or empty fields before saving.");
                return;
            }

            var dirtyRows = gridRows.Where(r => r.IsDirty).ToList();
            if (!dirtyRows.Any())
            {
                statusMessage = "No changes detected to save.";
                return;
            }

            isSaving = true;
            statusMessage = null;

            int savedCount = 0;
            try
            {
                foreach (var row in dirtyRows)
                {
                    var writableValues = row.CurrentValues
                        .Where(kvp => columnMetadata.TryGetValue(kvp.Key, out var meta) && !meta.IsGenerated && !meta.IsReadOnly)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

                    string? err = null;

                    if (row.IsNewRow || row.OriginalKeys == null || row.OriginalKeys.Count == 0)
                    {
                        err = await DbService.InsertRecordAsync(TableName, writableValues);
                    }
                    else
                    {
                        err = await DbService.SaveRecordAsync(TableName, row.OriginalKeys, writableValues);
                    }

                    if (err != null)
                    {
                        throw new Exception($"Error saving row ({string.Join(", ", row.CurrentValues.Where(c => columnMetadata.GetValueOrDefault(c.Key)?.IsPrimaryKey ?? false).Select(c => c.Value))}):\n\n{err}");
                    }

                    row.IsNewRow = false;
                    row.OriginalKeys = ExtractPrimaryKeyMap(row.CurrentValues);
                    row.OriginalValues = new Dictionary<string, string>(row.CurrentValues, StringComparer.OrdinalIgnoreCase);
                    savedCount++;
                }

                statusMessage = $"Successfully saved {savedCount} record(s)!";
            }
            catch (Exception ex)
            {
                ShowErrorDialog("Database Save Exception", ex.Message);
            }
            finally
            {
                isSaving = false;
                StateHasChanged();
            }
        }

        private void ShowErrorDialog(string title, string message)
        {
            messageBoxTitle = title;
            messageBoxMessage = message;
            showMessageBox = true;
        }
    }
}
