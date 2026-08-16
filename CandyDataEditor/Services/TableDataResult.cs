// Services/TableDataResult.cs
namespace CandyDataEditor.Services;

public class TableDataResult
{
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, string>> Rows { get; set; } = new();
}
