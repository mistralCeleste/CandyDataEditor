// Services/SqliteDataService.cs
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;

namespace CandyDataEditor.Services;

public class SqliteDataService
{
    public event Func<string, Task>? OnDatabasePathChanged;
    public SqliteEditorConfig Config { get; }

    public bool HasActiveDatabase => !string.IsNullOrEmpty(_dbPath);

    private string _dbPath;

    public SqliteDataService(SqliteEditorConfig config)
    {
        Config = config;
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "game_data.db");
        EnsureSampleDatabaseExists();
    }

    public string GetDatabasePath() => _dbPath;
    public string CurrentDatabasePath => _dbPath;

    public void SetDatabasePath(string newPath)
    {
        if (!string.IsNullOrWhiteSpace(newPath))
        {
            _dbPath = newPath;
            Config.AddRecentDatabase(newPath);
        }
    }

    public async Task SetDatabasePathAsync(string path)
    {
        _dbPath = path;
        Config.AddRecentDatabase(path);

        if (OnDatabasePathChanged != null)
        {
            await OnDatabasePathChanged.Invoke(path);
        }
    }

    /// <summary>
    /// Closes the currently active database and triggers path change to empty state.
    /// </summary>
    public async Task CloseDatabaseAsync()
    {
        _dbPath = string.Empty;

        if (OnDatabasePathChanged != null)
        {
            await OnDatabasePathChanged.Invoke(string.Empty);
        }
    }

    private string GetConnectionString()
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ConnectionString;
    }

    /// <summary>
    /// Gets all tables and views with their object type ('table' or 'view').
    /// </summary>
    public async Task<List<DbObjectInfo>> GetTablesAndViewsAsync()
    {
        var result = new List<DbObjectInfo>();
        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        string query = @"
            SELECT name, type FROM sqlite_master 
            WHERE type IN ('table', 'view') 
              AND name NOT LIKE 'sqlite_%' 
            ORDER BY name;";

        using var command = new SqliteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new DbObjectInfo
            {
                Name = reader.GetString(0),
                Type = reader.GetString(1)
            });
        }

        return result;
    }

    /// <summary>
    /// Fetches primary key values for all rows in a table to populate the left sidebar accordion.
    /// </summary>
    public async Task<List<Dictionary<string, string>>> GetRecordKeysAsync(string tableName, List<string> pkColumns)
    {
        var result = new List<Dictionary<string, string>>();
        if (!pkColumns.Any() || string.IsNullOrWhiteSpace(tableName)) return result;

        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        string cols = string.Join(", ", pkColumns.Select(c => $"\"{c.Replace("\"", "\"\"")}\""));
        string orderBy = string.Join(", ", pkColumns.Select(c => $"\"{c.Replace("\"", "\"\"")}\" ASC"));
        string sql = $"SELECT {cols} FROM \"{tableName.Replace("\"", "\"\"")}\" ORDER BY {orderBy} LIMIT {Config.QueryRowLimit};";

        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var keyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                keyMap[reader.GetName(i)] = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString() ?? "";
            }
            result.Add(keyMap);
        }

        return result;
    }

    /// <summary>
    /// Loads a single record matching the provided primary key dictionary.
    /// </summary>
    public async Task<Dictionary<string, string>?> GetRecordByKeysAsync(string tableName, Dictionary<string, string> keys)
    {
        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        var whereClauses = new List<string>();
        var command = connection.CreateCommand();

        int pIdx = 0;
        foreach (var kvp in keys)
        {
            string pName = $"@pk{pIdx++}";
            whereClauses.Add($"\"{kvp.Key.Replace("\"", "\"\"")}\" = {pName}");
            command.Parameters.AddWithValue(pName, (object?)kvp.Value ?? DBNull.Value);
        }

        command.CommandText = $"SELECT * FROM \"{tableName.Replace("\"", "\"\"")}\" WHERE {string.Join(" AND ", whereClauses)} LIMIT 1;";
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString() ?? "";
            }
            return row;
        }

        return null;
    }

    /// <summary>
    /// Returns the data grid for a specified table or view.
    /// </summary>
    public async Task<TableDataResult> GetTableDataAsync(string tableName)
    {
        var result = new TableDataResult();
        if (string.IsNullOrWhiteSpace(tableName)) return result;

        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        string query = $"SELECT * FROM \"{tableName.Replace("\"", "\"\"")}\" LIMIT {Config.QueryRowLimit};";

        using var command = new SqliteCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            result.Columns.Add(reader.GetName(i));
        }

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string colName = reader.GetName(i);
                string val = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i).ToString() ?? string.Empty;
                row[colName] = val;
            }
            result.Rows.Add(row);
        }

        return result;
    }

    /// <summary>
    /// Inspects schema metadata using PRAGMA table_xinfo to accurately catch Virtual and Stored Generated Columns.
    /// </summary>
    public async Task<Dictionary<string, ColumnMetadata>> GetColumnMetadataAsync(string tableName)
    {
        var metadata = new Dictionary<string, ColumnMetadata>(StringComparer.OrdinalIgnoreCase);

        using var connection = new SqliteConnection(GetConnectionString());
        await connection.OpenAsync();

        string typeCheckQuery = "SELECT type FROM sqlite_master WHERE name = @name;";
        using var typeCmd = new SqliteCommand(typeCheckQuery, connection);
        typeCmd.Parameters.AddWithValue("@name", tableName);
        string? objectType = (await typeCmd.ExecuteScalarAsync())?.ToString();
        bool isView = string.Equals(objectType, "view", StringComparison.OrdinalIgnoreCase);

        string pragmaSql = $"PRAGMA table_xinfo(\"{tableName.Replace("\"", "\"\"")}\");";
        using var command = new SqliteCommand(pragmaSql, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            string colName = reader.GetString(1);
            int isPk = reader.GetInt32(5);
            int hiddenValue = reader.FieldCount > 6 ? reader.GetInt32(6) : 0; // 2 = VIRTUAL generated, 3 = STORED generated

            bool isGenerated = hiddenValue == 2 || hiddenValue == 3;

            metadata[colName] = new ColumnMetadata
            {
                ColumnName = colName,
                IsPrimaryKey = isPk > 0,
                IsReadOnly = isView || isGenerated,
                IsGenerated = isGenerated
            };
        }

        return metadata;
    }

    /// <summary>
    /// Evaluates generated column values for a set of field inputs without saving to the database.
    /// </summary>
    public async Task<Dictionary<string, string>> RecalculateGeneratedFieldsAsync(string tableName, Dictionary<string, string> currentValues)
    {
        var generatedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(tableName) || currentValues == null || !currentValues.Any())
            return generatedValues;

        try
        {
            using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var meta = await GetColumnMetadataAsync(tableName);
            var genCols = meta.Where(m => m.Value.IsGenerated).Select(m => m.Key).ToList();

            if (!genCols.Any()) return generatedValues;

            var writableValues = currentValues
                .Where(kvp => meta.TryGetValue(kvp.Key, out var m) && !m.IsGenerated && !m.IsReadOnly)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            using var transaction = await connection.BeginTransactionAsync();
            var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;

            var cols = new List<string>();
            var paramsList = new List<string>();
            int idx = 0;

            foreach (var kvp in writableValues)
            {
                string pName = $"@p{idx++}";
                cols.Add($"\"{kvp.Key.Replace("\"", "\"\"")}\"");
                paramsList.Add(pName);
                command.Parameters.AddWithValue(pName, (object?)kvp.Value ?? DBNull.Value);
            }

            string selectGenCols = string.Join(", ", genCols.Select(c => $"\"{c.Replace("\"", "\"\"")}\""));
            command.CommandText = $"INSERT INTO \"{tableName.Replace("\"", "\"\"")}\" ({string.Join(", ", cols)}) VALUES ({string.Join(", ", paramsList)}) RETURNING {selectGenCols};";

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string colName = reader.GetName(i);
                    string val = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i).ToString() ?? string.Empty;
                    generatedValues[colName] = val;
                }
            }

            await transaction.RollbackAsync();
        }
        catch
        {
            // Fail silently on transient/partial key validation errors during typing
        }

        return generatedValues;
    }

    /// <summary>
    /// Saves changes to an existing record.
    /// </summary>
    public async Task<string?> SaveRecordAsync(string tableName, Dictionary<string, string> originalKeys, Dictionary<string, string> updatedValues)
    {
        try
        {
            using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var meta = await GetColumnMetadataAsync(tableName);
            var setClauses = new List<string>();
            var command = connection.CreateCommand();

            int paramIndex = 0;
            foreach (var kvp in updatedValues)
            {
                string colName = kvp.Key;

                if (meta.TryGetValue(colName, out var colMeta) && (colMeta.IsGenerated || colMeta.IsReadOnly))
                {
                    continue;
                }

                string paramName = $"@p{paramIndex++}";
                setClauses.Add($"\"{colName.Replace("\"", "\"\"")}\" = {paramName}");
                command.Parameters.AddWithValue(paramName, (object?)kvp.Value ?? DBNull.Value);
            }

            if (!setClauses.Any()) return null;

            var whereClauses = new List<string>();
            foreach (var kvp in originalKeys)
            {
                string colName = kvp.Key;
                string paramName = $"@w{paramIndex++}";
                whereClauses.Add($"\"{colName.Replace("\"", "\"\"")}\" = {paramName}");
                command.Parameters.AddWithValue(paramName, (object?)kvp.Value ?? DBNull.Value);
            }

            string sql = $"UPDATE \"{tableName.Replace("\"", "\"\"")}\" SET {string.Join(", ", setClauses)} WHERE {string.Join(" AND ", whereClauses)};";
            command.CommandText = sql;

            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (SqliteException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Inserts a new record into the database.
    /// </summary>
    public async Task<string?> InsertRecordAsync(string tableName, Dictionary<string, string> newValues)
    {
        try
        {
            using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var meta = await GetColumnMetadataAsync(tableName);
            var command = connection.CreateCommand();
            var cols = new List<string>();
            var paramsList = new List<string>();

            int idx = 0;
            foreach (var kvp in newValues)
            {
                string colName = kvp.Key;

                if (meta.TryGetValue(colName, out var colMeta) && (colMeta.IsGenerated || colMeta.IsReadOnly))
                {
                    continue;
                }

                string pName = $"@p{idx++}";
                cols.Add($"\"{colName.Replace("\"", "\"\"")}\"");
                paramsList.Add(pName);
                command.Parameters.AddWithValue(pName, (object?)kvp.Value ?? DBNull.Value);
            }

            if (!cols.Any()) return "No writable columns available to insert.";

            command.CommandText = $"INSERT INTO \"{tableName.Replace("\"", "\"\"")}\" ({string.Join(", ", cols)}) VALUES ({string.Join(", ", paramsList)});";
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (SqliteException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Deletes a record from the database.
    /// </summary>
    public async Task<string?> DeleteRecordAsync(string tableName, Dictionary<string, string> keys)
    {
        try
        {
            using var connection = new SqliteConnection(GetConnectionString());
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            var whereClauses = new List<string>();

            int idx = 0;
            foreach (var kvp in keys)
            {
                string pName = $"@w{idx++}";
                whereClauses.Add($"\"{kvp.Key.Replace("\"", "\"\"")}\" = {pName}");
                command.Parameters.AddWithValue(pName, (object?)kvp.Value ?? DBNull.Value);
            }

            command.CommandText = $"DELETE FROM \"{tableName.Replace("\"", "\"\"")}\" WHERE {string.Join(" AND ", whereClauses)};";
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (SqliteException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Exports specified tables and views into TSV, CSV, XML, or JSON files cleanly without duplicating rows.
    /// </summary>
    public async Task ExportDataFilesAsync(List<string> tableNames, string format, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        foreach (var table in tableNames)
        {
            var data = await GetTableDataAsync(table);
            string filePath = Path.Combine(outputDirectory, $"{table}.{format.ToLower()}");

            if (format.Equals("tsv", StringComparison.OrdinalIgnoreCase) || format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                bool isTsv = format.Equals("tsv", StringComparison.OrdinalIgnoreCase);
                char sep = isTsv ? '\t' : ',';
                var sb = new StringBuilder();

                // Header Row
                sb.AppendLine(string.Join(sep, data.Columns));

                // Data Rows
                foreach (var row in data.Rows)
                {
                    var values = data.Columns.Select(c =>
                    {
                        if (!row.TryGetValue(c, out var val) || string.IsNullOrEmpty(val))
                            return "\"\"";

                        string cleanValue = val;
                        cleanValue = Regex.Replace(cleanValue, @"[\r\n\u2028\u2029]+", "<br>");
                        cleanValue = cleanValue.Replace("\"", "\"\"");
                        cleanValue = cleanValue.Trim();
                        return $"\"{cleanValue}\"";
                    });

                    sb.AppendLine(string.Join(sep, values));
                }

                await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
            }
            else if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                string json = JsonSerializer.Serialize(data.Rows, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
            }
            else if (format.Equals("xml", StringComparison.OrdinalIgnoreCase))
            {
                string rootTag = SanitizeXmlElementName(table);

                var xDoc = new XDocument(
                    new XElement(rootTag + "List",
                        data.Rows.Select(r => new XElement("Record",
                            r.Select(kvp => new XElement(SanitizeXmlElementName(kvp.Key), kvp.Value ?? ""))
                        ))
                    )
                );

                xDoc.Save(filePath);
            }
        }
    }

    /// <summary>
    /// Exports an interactive HTML Bundle allowing instant record card searching by Full ID or content.
    /// </summary>
    public async Task ExportHtmlCardsBundleAsync(List<string> tableNames, string outputDirectory)
    {
        string exportPath = Path.Combine(outputDirectory, "GameCards.html");
        Directory.CreateDirectory(outputDirectory);

        var allRecords = new List<Dictionary<string, string>>();
        foreach (var table in tableNames)
        {
            var data = await GetTableDataAsync(table);
            allRecords.AddRange(data.Rows);
        }

        string jsonRecords = JsonSerializer.Serialize(allRecords);

        string htmlTemplate = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Game Record Cards Viewer</title>
    <style>
        body {{ font-family: system-ui, -apple-system, sans-serif; padding: 20px; background: #f8f9fa; color: #212529; }}
        #search {{ padding: 12px; width: 100%; max-width: 450px; margin-bottom: 20px; font-size: 16px; border: 1px solid #ced4da; border-radius: 6px; }}
        .card {{ background: white; border: 1px solid #dee2e6; border-radius: 8px; padding: 16px; margin-bottom: 12px; box-shadow: 0 2px 4px rgba(0,0,0,0.05); }}
        .card h3 {{ margin-top: 0; color: #0d6efd; }}
        .field {{ margin-bottom: 6px; }}
        .field-name {{ font-weight: bold; color: #495057; }}
    </style>
</head>
<body>
    <h2>🔍 Game Data Interactive Card Viewer</h2>
    <input type='text' id='search' placeholder='Type Full ID or Title (e.g. LOC-001A)...' oninput='filterCards()' />
    <div id='cardsContainer'></div>

    <script>
        const records = {jsonRecords};
        function filterCards() {{
            const q = document.getElementById('search').value.toLowerCase().trim();
            const container = document.getElementById('cardsContainer');
            container.innerHTML = '';

            if (!q) return;

            records.filter(r => {{
                return Object.values(r).some(v => String(v).toLowerCase().includes(q));
            }}).forEach(r => {{
                const card = document.createElement('div');
                card.className = 'card';
                let html = `<h3>${{r['Full Id'] || r['Id'] || 'Record'}}</h3>`;
                for (let k in r) {{
                    html += `<div class='field'><span class='field-name'>${{k}}:</span> ${{r[k]}}</div>`;
                }}
                card.innerHTML = html;
                container.appendChild(card);
            }});
        }}
    </script>
</body>
</html>";

        await File.WriteAllTextAsync(exportPath, htmlTemplate, Encoding.UTF8);
    }

    /// <summary>
    /// Sanitizes column headers to valid XML element tag names.
    /// </summary>
    private static string SanitizeXmlElementName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Field";

        string sanitized = Regex.Replace(name, @"^[^\p{L}_]+", "");
        sanitized = Regex.Replace(sanitized, @"[^\p{L}\p{Nd}_.-]", "_");

        if (string.IsNullOrEmpty(sanitized) || char.IsDigit(sanitized[0]) || sanitized.StartsWith("xml", StringComparison.OrdinalIgnoreCase))
        {
            sanitized = "_" + sanitized;
        }

        return sanitized;
    }

    private void EnsureSampleDatabaseExists()
    {
        if (File.Exists(_dbPath)) return;

        using var connection = new SqliteConnection(GetConnectionString());
        connection.Open();

        string createTableSql = @"
            CREATE TABLE IF NOT EXISTS Items (
                Id TEXT NOT NULL,
                Version INTEGER NOT NULL,
                Title TEXT,
                MarkdownContent TEXT,
                PRIMARY KEY (Id, Version)
            );

            INSERT INTO Items (Id, Version, Title, MarkdownContent) VALUES 
            ('MAP-001', 1, 'Whirlpool Bluff', '# Setup\n~ [place] MAP-001\n~ [mob] FOE-003\n\nEnemies have ==defense== [defense].'),
            ('EVT-004', 1, 'Moonlit Pearl Wreck', '# Actions\n@ Wreckage\nBeastfolk **scavengers** prowl the wreck.'),
            ('PUZ-001A', 1, 'Room Blocks', '# Triggers\n@ 503\nTwo blocks and two symbols are shown.');";

        using var command = new SqliteCommand(createTableSql, connection);
        command.ExecuteNonQuery();
    }
}