using System.Collections.Concurrent;
using System.Reflection;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Precedence tier of a project parser, from strongest to weakest manifest signal.
/// </summary>
public enum ParserTier
{
    /// <summary>Dedicated build manifest that only exists for that language (.csproj, pom.xml, go.mod, Cargo.toml).</summary>
    Primary = 0,

    /// <summary>Manifest that commonly co-exists with other projects for tooling or bundled frontends (package.json).</summary>
    Secondary = 1,

    /// <summary>Folders of schema/data files (e.g. SQL scripts).</summary>
    Data = 2,

    /// <summary>Parsers that declare no precedence.</summary>
    Default = 3,

    /// <summary>Evaluated only after every other parser failed to claim a directory.</summary>
    Fallback = 4
}

/// <summary>
/// Declares how a project parser ranks against other parsers that match the same directory.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ProjectParserAttribute : Attribute
{
    public ParserTier Tier { get; init; } = ParserTier.Default;

    /// <summary>Project types this parser wins over (and removes) when both match the same directory.</summary>
    public string[] Supersedes { get; init; } = [];
}

/// <summary>
/// Resolves <see cref="ProjectParserAttribute"/> metadata and produces a deterministic parser order:
/// by tier, then superseding parsers before the ones they supersede, then by project type.
/// </summary>
public static class ProjectParserPrecedence
{
    private static readonly ProjectParserAttribute DefaultAttribute = new();
    private static readonly ConcurrentDictionary<Type, ProjectParserAttribute> Cache = new();

    public static ProjectParserAttribute Get(Type parserType) =>
        Cache.GetOrAdd(parserType, t => t.GetCustomAttribute<ProjectParserAttribute>(inherit: false) ?? DefaultAttribute);

    public static ParserTier GetTier(IProjectParser parser) => Get(parser.GetType()).Tier;

    public static IReadOnlyList<IProjectParser> Order(IEnumerable<IProjectParser> parsers)
    {
        var nodes = parsers.ToList();
        var inDegree = nodes.ToDictionary(p => p, _ => 0);
        var edges = nodes.ToDictionary(p => p, _ => new List<IProjectParser>());

        foreach (var winner in nodes)
        {
            foreach (var supersededType in Get(winner.GetType()).Supersedes)
            {
                foreach (var loser in nodes.Where(n => n != winner &&
                             string.Equals(n.ProjectType, supersededType, StringComparison.OrdinalIgnoreCase)))
                {
                    edges[winner].Add(loser);
                    inDegree[loser]++;
                }
            }
        }

        var ready = new PriorityQueue<IProjectParser, (ParserTier Tier, string Type, string Name)>(KeyComparer);
        foreach (var node in nodes.Where(n => inDegree[n] == 0))
        {
            ready.Enqueue(node, SortKey(node));
        }

        var result = new List<IProjectParser>(nodes.Count);
        while (ready.TryDequeue(out var next, out _))
        {
            result.Add(next);
            foreach (var loser in edges[next])
            {
                if (--inDegree[loser] == 0)
                {
                    ready.Enqueue(loser, SortKey(loser));
                }
            }
        }

        if (result.Count != nodes.Count)
        {
            var cyclic = nodes.Where(n => inDegree[n] > 0).Select(n => n.ProjectType);
            throw new InvalidOperationException(
                $"[ProjectParserPrecedence] Cyclic 'Supersedes' declarations between project parsers: {string.Join(", ", cyclic)}");
        }

        return result;
    }

    /// <summary>Removes parsers superseded by another parser in the same match set, preserving order.</summary>
    public static void RemoveSuperseded(List<IProjectParser> matching)
    {
        if (matching.Count < 2) return;

        var superseded = new HashSet<string>(
            matching.SelectMany(p => Get(p.GetType()).Supersedes),
            StringComparer.OrdinalIgnoreCase);

        if (superseded.Count > 0)
        {
            matching.RemoveAll(p => superseded.Contains(p.ProjectType));
        }
    }

    private static readonly Comparer<(ParserTier Tier, string Type, string Name)> KeyComparer =
        Comparer<(ParserTier Tier, string Type, string Name)>.Create((a, b) =>
        {
            var c = a.Tier.CompareTo(b.Tier);
            if (c != 0) return c;
            c = string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
        });

    private static (ParserTier Tier, string Type, string Name) SortKey(IProjectParser parser) =>
        (GetTier(parser), parser.ProjectType, parser.GetType().FullName ?? string.Empty);
}
