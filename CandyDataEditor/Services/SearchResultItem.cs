namespace CandyDataEditor.Services
{
    public class SearchResultItem
    {
        public string TableName { get; set; } = string.Empty;
        public Dictionary<string, string> PrimaryKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string MatchingColumn { get; set; } = string.Empty;
        public string SnippetPrefix { get; set; } = string.Empty;
        public string MatchTerm { get; set; } = string.Empty;
        public string SnippetSuffix { get; set; } = string.Empty;
    }
}
