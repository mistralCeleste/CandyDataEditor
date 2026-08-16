// Services/ColumnMetadata.cs
namespace CandyDataEditor.Services;

public class ColumnMetadata
{
    public string ColumnName { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsGenerated { get; set; }
}
