// Services/DbObjectInfo.cs
namespace CandyDataEditor.Services;

public class DbObjectInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "table"; // 'table' or 'view'
}
