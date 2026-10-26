using System.Text.RegularExpressions;

namespace CodeExplorer.Parser.Rust;

/// <summary>
/// Approximates which source files of a dependency crate are reachable from a binary target,
/// by following <c>use</c> paths and inline <c>crate_name::module::item</c> paths at file granularity.
/// Used to attribute library-level semantics (topics, queues) only to the binaries that can actually reach them.
/// </summary>
public static class RustModuleReachability
{
    private const int MaxFiles = 4000;
    private const int MaxBinaryModuleDepth = 8;

    private static readonly Regex UseStatementRegex = new(@"\buse\s+([^;]+);", RegexOptions.Compiled);
    private static readonly Regex InlinePathRegex = new(
        @"(?<![A-Za-z0-9_:])([A-Za-z_][A-Za-z0-9_]*)((?:\s*::\s*[A-Za-z_][A-Za-z0-9_]*)+)",
        RegexOptions.Compiled);
    private static readonly Regex ModDeclarationRegex = new(
        @"^\s*(?:pub(?:\([^)]*\))?\s+)?mod\s+([A-Za-z_][A-Za-z0-9_]*)\s*;",
        RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex LineCommentRegex = new(@"//[^\n]*", RegexOptions.Compiled);
    private static readonly Regex AliasRegex = new(@"\s+as\s+[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Returns the entry file plus every local module file declared through <c>mod name;</c> (recursively).
    /// </summary>
    public static IReadOnlyList<string> CollectBinaryModuleFiles(string entryPointPath)
    {
        var result = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string File, int Depth)>();
        queue.Enqueue((Path.GetFullPath(entryPointPath), 0));

        while (queue.Count > 0)
        {
            var (file, depth) = queue.Dequeue();
            if (!visited.Add(file) || !File.Exists(file))
            {
                continue;
            }

            result.Add(file);
            if (depth >= MaxBinaryModuleDepth)
            {
                continue;
            }

            var childDir = GetChildModuleDirectory(file, isCrateRoot: depth == 0);
            foreach (Match match in ModDeclarationRegex.Matches(ReadSource(file)))
            {
                var name = match.Groups[1].Value;
                var candidate = ResolveChildModuleFile(childDir, name);
                if (candidate != null)
                {
                    queue.Enqueue((candidate, depth + 1));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the set of files in <paramref name="crateRootFile"/>'s crate that are reachable from
    /// <paramref name="consumerFiles"/> referencing the crate as <paramref name="crateIdentifiers"/>.
    /// Returns null when the consumer references the crate in a way that cannot be resolved to specific files.
    /// </summary>
    public static IReadOnlySet<string>? ComputeReachableFiles(
        IEnumerable<string> consumerFiles,
        IReadOnlyCollection<string> crateIdentifiers,
        string crateRootFile)
    {
        if (!File.Exists(crateRootFile))
        {
            return null;
        }

        var srcRoot = Path.GetDirectoryName(Path.GetFullPath(crateRootFile)) ?? string.Empty;
        var rootFile = Path.GetFullPath(crateRootFile);
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        void Enqueue(string file)
        {
            if (reachable.Count < MaxFiles && reachable.Add(file))
            {
                queue.Enqueue(file);
            }
        }

        foreach (var consumer in consumerFiles)
        {
            foreach (var segments in ExtractPaths(ReadSource(consumer)))
            {
                if (segments.Count > 0 && crateIdentifiers.Contains(segments[0]))
                {
                    Enqueue(ResolveModuleFile(srcRoot, rootFile, segments.Skip(1).ToList()));
                }
            }
        }

        if (reachable.Count == 0)
        {
            return null;
        }

        while (queue.Count > 0)
        {
            var file = queue.Dequeue();
            var source = ReadSource(file);
            var currentModule = GetModulePath(srcRoot, rootFile, file);
            var childModules = ModDeclarationRegex.Matches(source)
                .Select(m => m.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var segments in ExtractPaths(source))
            {
                var modulePath = ToCrateModulePath(segments, currentModule, childModules, crateIdentifiers);
                if (modulePath != null)
                {
                    Enqueue(ResolveModuleFile(srcRoot, rootFile, modulePath));
                }
            }
        }

        return reachable;
    }

    private static List<string>? ToCrateModulePath(
        List<string> segments,
        IReadOnlyList<string> currentModule,
        IReadOnlySet<string> childModules,
        IReadOnlyCollection<string> crateIdentifiers)
    {
        if (segments.Count == 0)
        {
            return null;
        }

        var head = segments[0];
        if (head == "crate" || crateIdentifiers.Contains(head))
        {
            return segments.Skip(1).ToList();
        }

        if (head == "self")
        {
            return currentModule.Concat(segments.Skip(1)).ToList();
        }

        if (head == "super")
        {
            var module = currentModule.ToList();
            var index = 0;
            while (index < segments.Count && segments[index] == "super")
            {
                if (module.Count > 0)
                {
                    module.RemoveAt(module.Count - 1);
                }
                index++;
            }
            return module.Concat(segments.Skip(index)).ToList();
        }

        if (childModules.Contains(head))
        {
            return currentModule.Concat(segments).ToList();
        }

        return null;
    }

    private static string ResolveModuleFile(string srcRoot, string rootFile, IReadOnlyList<string> modulePath)
    {
        for (var length = modulePath.Count; length > 0; length--)
        {
            var relative = Path.Combine(modulePath.Take(length).ToArray());
            var asFile = Path.Combine(srcRoot, relative + ".rs");
            if (File.Exists(asFile))
            {
                return asFile;
            }

            var asModFile = Path.Combine(srcRoot, relative, "mod.rs");
            if (File.Exists(asModFile))
            {
                return asModFile;
            }
        }

        return rootFile;
    }

    private static IReadOnlyList<string> GetModulePath(string srcRoot, string rootFile, string file)
    {
        if (string.Equals(file, rootFile, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var relative = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
        var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var last = segments[^1];
        if (last.Equals("mod.rs", StringComparison.OrdinalIgnoreCase))
        {
            segments.RemoveAt(segments.Count - 1);
        }
        else
        {
            segments[^1] = Path.GetFileNameWithoutExtension(last);
        }

        return segments;
    }

    private static string GetChildModuleDirectory(string file, bool isCrateRoot)
    {
        var directory = Path.GetDirectoryName(file) ?? string.Empty;
        var fileName = Path.GetFileName(file);
        return isCrateRoot || fileName.Equals("mod.rs", StringComparison.OrdinalIgnoreCase)
            ? directory
            : Path.Combine(directory, Path.GetFileNameWithoutExtension(fileName));
    }

    private static string? ResolveChildModuleFile(string directory, string name)
    {
        var asFile = Path.Combine(directory, name + ".rs");
        if (File.Exists(asFile))
        {
            return asFile;
        }

        var asModFile = Path.Combine(directory, name, "mod.rs");
        return File.Exists(asModFile) ? asModFile : null;
    }

    private static IEnumerable<List<string>> ExtractPaths(string source)
    {
        foreach (Match useMatch in UseStatementRegex.Matches(source))
        {
            foreach (var path in ExpandUseTree(useMatch.Groups[1].Value))
            {
                yield return path;
            }
        }

        var withoutUses = UseStatementRegex.Replace(source, string.Empty);
        foreach (Match match in InlinePathRegex.Matches(withoutUses))
        {
            var tail = match.Groups[2].Value
                .Split("::", StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim());
            yield return [match.Groups[1].Value, .. tail];
        }
    }

    /// <summary>
    /// Expands a use tree such as <c>a::b::{c, d::{e, f}}</c> into individual segment paths.
    /// </summary>
    public static List<List<string>> ExpandUseTree(string tree)
    {
        var results = new List<List<string>>();
        var withoutAliases = AliasRegex.Replace(tree, string.Empty);
        Expand(WhitespaceRegex.Replace(withoutAliases, string.Empty), [], results);
        return results;
    }

    private static void Expand(string tree, List<string> prefix, List<List<string>> results)
    {
        if (tree.Length == 0)
        {
            return;
        }

        var braceIndex = tree.IndexOf('{');
        if (braceIndex < 0)
        {
            var segments = prefix.Concat(tree.Split("::", StringSplitOptions.RemoveEmptyEntries))
                .Where(s => s != "*")
                .ToList();
            if (segments.Count > 0 && segments[^1] == "self")
            {
                segments.RemoveAt(segments.Count - 1);
            }
            if (segments.Count > 0)
            {
                results.Add(segments);
            }
            return;
        }

        var head = tree[..braceIndex].TrimEnd(':');
        var closing = FindMatchingBrace(tree, braceIndex);
        if (closing < 0)
        {
            return;
        }

        var nextPrefix = prefix.Concat(head.Split("::", StringSplitOptions.RemoveEmptyEntries)).ToList();
        foreach (var part in SplitTopLevel(tree[(braceIndex + 1)..closing]))
        {
            Expand(part, nextPrefix, results);
        }
    }

    private static int FindMatchingBrace(string text, int openIndex)
    {
        var depth = 0;
        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static IEnumerable<string> SplitTopLevel(string text)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;
            else if (text[i] == ',' && depth == 0)
            {
                yield return text[start..i];
                start = i + 1;
            }
        }
        if (start < text.Length)
        {
            yield return text[start..];
        }
    }

    private static string ReadSource(string file)
    {
        try
        {
            return LineCommentRegex.Replace(File.ReadAllText(file), string.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
