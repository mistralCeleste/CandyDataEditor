namespace CandyDataEditor.Services
{
    public static class SpellSuggestService
    {
        /// <summary>
        /// Finds fast, accurate suggestions for a misspelled word using Levenshtein distance.
        /// </summary>
        public static List<string> GetSuggestions(string misspelledWord, IEnumerable<string> dictionary, int maxSuggestions = 5)
        {
            if (string.IsNullOrWhiteSpace(misspelledWord)) return new List<string>();

            string target = misspelledWord.ToLowerInvariant().Trim();
            char firstChar = target[0];

            // 1. Filter dictionary to words starting with same or adjacent letter to narrow candidate set from 370,000 down to ~15,000
            var candidates = dictionary
                .Where(w => !string.IsNullOrEmpty(w) && Math.Abs(w.Length - target.Length) <= 3)
                .Where(w => char.ToLowerInvariant(w[0]) == firstChar || LevenshteinDistance(target, w.ToLowerInvariant()) <= 2);

            // 2. Rank candidates by Levenshtein distance
            return candidates
                .Select(w => new { Word = w, Distance = LevenshteinDistance(target, w.ToLowerInvariant()) })
                .Where(x => x.Distance <= 3) // Max 3 edits (e.g. typos, missing letters, swapped chars)
                .OrderBy(x => x.Distance)
                .ThenBy(x => Math.Abs(x.Word.Length - target.Length))
                .Select(x => x.Word)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxSuggestions)
                .ToList();
        }

        /// <summary>
        /// Fast Levenshtein distance matrix computation.
        /// </summary>
        private static int LevenshteinDistance(string source, string target)
        {
            if (source == target) return 0;
            if (source.Length == 0) return target.Length;
            if (target.Length == 0) return source.Length;

            int[] v0 = new int[target.Length + 1];
            int[] v1 = new int[target.Length + 1];

            for (int i = 0; i <= target.Length; i++) v0[i] = i;

            for (int i = 0; i < source.Length; i++)
            {
                v1[0] = i + 1;

                for (int j = 0; j < target.Length; j++)
                {
                    int cost = (source[i] == target[j]) ? 0 : 1;
                    v1[j + 1] = Math.Min(Math.Min(v1[j] + 1, v0[j + 1] + 1), v0[j] + cost);
                }

                for (int j = 0; j <= target.Length; j++) v0[j] = v1[j];
            }

            return v1[target.Length];
        }
    }
}
