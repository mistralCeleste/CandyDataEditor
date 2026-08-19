// Services/FontLigatureService.cs
using Typography.OpenFont;

namespace CandyDataEditor.Services
{
    public class FontLigatureService
    {
        public List<string> ExtractLigatures(string fontFilePath, string featureTable = "liga")
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(fontFilePath) || !File.Exists(fontFilePath))
                return results.ToList();

            using var stream = File.OpenRead(fontFilePath);
            var reader = new OpenFontReader();
            Typeface typeface = reader.Read(stream);

            var gsub = typeface?.GSUBTable;
            if (gsub?.FeatureList?.featureTables == null)
                return results.ToList();

            // Find target feature table (e.g. "liga")
            var iconFeature = gsub.FeatureList.featureTables
                .FirstOrDefault(f => f.TagName.Equals(featureTable, StringComparison.OrdinalIgnoreCase));

            if (iconFeature == null)
                return results.ToList();

            // Pre-build Unicode mapping cache once per scan
            var glyphToUnicodeMap = GetGlyphToUnicodeMap(typeface);

            foreach (ushort lookupIndex in iconFeature.LookupListIndices)
            {
                if (lookupIndex >= gsub.LookupList.Count) continue;

                var lookup = gsub.LookupList[lookupIndex];
                if (lookup?.SubTables == null) continue;

                foreach (var sub in lookup.SubTables)
                {
                    // Access nested LigatureSetTables via reflection on private subtable implementations
                    var ligSetTablesProp = sub.GetType().GetProperty("LigatureSetTables");
                    if (ligSetTablesProp?.GetValue(sub) is not Array ligSetTables) continue;

                    foreach (var ligSet in ligSetTables)
                    {
                        var ligaturesProp = ligSet.GetType().GetProperty("Ligatures");
                        if (ligaturesProp?.GetValue(ligSet) is not Array ligatures) continue;

                        foreach (var lig in ligatures)
                        {
                            var type = lig.GetType();
                            var componentGlyphsField = type.GetField("ComponentGlyphs");
                            var glyphIdField = type.GetField("GlyphId");

                            if (componentGlyphsField == null || glyphIdField == null) continue;

                            var componentGlyphs = componentGlyphsField.GetValue(lig) as ushort[];
                            var glyphId = glyphIdField.GetValue(lig) as ushort?;

                            if (componentGlyphs == null || componentGlyphs.Length == 0 || glyphId == null)
                                continue;

                            // Trim the trailing 'bracketright' glyph injected by FontForge
                            ushort[] trimmed = componentGlyphs.Take(componentGlyphs.Length - 1).ToArray();
                            var parts = new List<char>();

                            foreach (ushort gId in trimmed)
                            {
                                var g = typeface.GetGlyph(gId);
                                if (g == null || !g.IsCffGlyph) continue;

                                var cff = g.GetCff1GlyphData();
                                if (cff?.Name != null)
                                {
                                    if (cff.Name.Length == 1)
                                    {
                                        parts.Add(cff.Name[0]);
                                    }
                                    else if (cff.Name.Length > 1)
                                    {
                                        // Fallback to cmap unicode code point for substituted word names
                                        if (glyphToUnicodeMap.TryGetValue(gId, out uint unicodeHex))
                                        {
                                            parts.Add((char)unicodeHex);
                                        }
                                    }
                                }
                            }

                            string ligatureKey = string.Join("", parts);
                            if (!string.IsNullOrWhiteSpace(ligatureKey))
                            {
                                results.Add(ligatureKey);
                            }
                        }
                    }
                }
            }

            return results.OrderBy(s => s).ToList();
        }

        /// <summary>
        /// Builds a reverse dictionary mapping Glyph IDs to Unicode codepoints (including PUA e.g. 0xE00E).
        /// </summary>
        public Dictionary<ushort, uint> GetGlyphToUnicodeMap(Typeface typeface)
        {
            var glyphToUnicode = new Dictionary<ushort, uint>();

            if (typeface?.CmapTable == null)
                return glyphToUnicode;

            var unicodes = new List<uint>();
            typeface.CmapTable.CollectUnicode(unicodes);

            foreach (uint unicode in unicodes)
            {
                ushort glyphId = typeface.GetGlyphIndex((int)unicode);

                if (glyphId > 0 && !glyphToUnicode.ContainsKey(glyphId))
                {
                    glyphToUnicode[glyphId] = unicode;
                }
            }

            return glyphToUnicode;
        }
    }
}
