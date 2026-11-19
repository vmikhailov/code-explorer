using System.Collections.Concurrent;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Index of all registered <see cref="ISharedFileParser"/> implementations keyed by file extension.
/// </summary>
public static class SharedFileParserRegistry
{
    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<string, ISharedFileParser[]> ByExtension = new(StringComparer.OrdinalIgnoreCase);
    private static ISharedFileParser[] _prefixed = [];
    private static ISharedFileParser[] _all = [];

    /// <summary>Upper bound for files read by shared parsers.</summary>
    public const long MaxFileSizeBytes = 1024 * 1024;

    public static IReadOnlyList<ISharedFileParser> All => _all;

    public static void Register(ISharedFileParser parser)
    {
        lock (Gate)
        {
            if (_all.Any(p => p.GetType() == parser.GetType())) return;

            _all = [.. _all, parser];

            foreach (var ext in parser.Extensions)
            {
                ByExtension.AddOrUpdate(ext, _ => [parser], (_, existing) => [.. existing, parser]);
            }

            if (parser.FileNamePrefixes.Count > 0)
            {
                _prefixed = [.. _prefixed, parser];
            }
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            ByExtension.Clear();
            _prefixed = [];
            _all = [];
        }
    }

    public static IReadOnlyList<ISharedFileParser> GetParsers(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return [];

        var name = Path.GetFileName(fileName);
        var lower = name.ToLowerInvariant();
        var result = new List<ISharedFileParser>();

        if (ByExtension.TryGetValue(Path.GetExtension(lower), out var byExt))
        {
            result.AddRange(byExt);
        }

        foreach (var parser in _prefixed)
        {
            if (!result.Contains(parser) && parser.FileNamePrefixes.Any(p => lower.StartsWith(p)))
            {
                result.Add(parser);
            }
        }

        result.RemoveAll(p => !p.CanParse(name));
        return result;
    }

    public static bool CanParse(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var name = Path.GetFileName(fileName);
        var ext = Path.GetExtension(name);
        if (!string.IsNullOrEmpty(ext) && ByExtension.TryGetValue(ext, out var byExt))
        {
            for (var i = 0; i < byExt.Length; i++)
            {
                if (byExt[i].CanParse(name)) return true;
            }
        }

        if (_prefixed.Length > 0)
        {
            var lower = name.ToLowerInvariant();
            for (var i = 0; i < _prefixed.Length; i++)
            {
                var p = _prefixed[i];
                if (p.FileNamePrefixes.Any(pref => lower.StartsWith(pref, StringComparison.Ordinal)) && p.CanParse(name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Runs every matching parser over content that has already been read.</summary>
    public static IReadOnlyList<IProjectEnrichment> Parse(string fullPath, string content)
    {
        var parsers = GetParsers(fullPath);
        if (parsers.Count == 0) return [];

        var context = new SharedFileContext(fullPath, Path.GetFileName(fullPath), content);
        var results = new List<IProjectEnrichment>(parsers.Count);
        foreach (var parser in parsers)
        {
            try
            {
                var enrichment = parser.Parse(context);
                if (enrichment != null) results.Add(enrichment);
            }
            catch
            {
                // A malformed file must not break indexing of the rest of the project.
            }
        }

        return results;
    }

    /// <summary>Reads the file once and parses it. Used outside the pipeline (tests, tools).</summary>
    public static IReadOnlyList<IProjectEnrichment> ParseFile(string fullPath) =>
        File.Exists(fullPath) ? Parse(fullPath, File.ReadAllText(fullPath)) : [];
}
