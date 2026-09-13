using System.Collections.Concurrent;
using System.Reflection;

namespace CodeExplorer.Core.Mcp;

public static class Queries
{
    private static readonly Assembly Assembly = typeof(Queries).Assembly;
    private static readonly string[] ResourceNames = Assembly.GetManifestResourceNames();
    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string Get(string name)
    {
        return Cache.GetOrAdd(name, n =>
        {
            var match = ResourceNames.FirstOrDefault(r => 
                r.EndsWith($".Queries.{n}.cypher", StringComparison.OrdinalIgnoreCase) ||
                r.EndsWith($".{n}.cypher", StringComparison.OrdinalIgnoreCase)
            );

            if (match == null)
            {
                throw new FileNotFoundException($"Embedded cypher query resource '{n}' not found.");
            }

            using var stream = Assembly.GetManifestResourceStream(match);
            if (stream == null)
            {
                throw new FileNotFoundException($"Failed to load manifest resource stream for '{match}'.");
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Trim();
        });
    }

    public record BuiltInQueryInfo(string Name, string Description, string Category);

    private static readonly Dictionary<string, (string Description, string Category)> KnownMetadata = new(StringComparer.OrdinalIgnoreCase)
    {
        ["get_architecture_map_workspace"] = ("Hierarchical workspace architecture map (projects, languages, dependencies, ingress, egress)", "Architecture"),
        ["get_architecture_map_project"] = ("Project-level architecture map including dependencies and endpoints", "Architecture"),
        ["get_project_entry_points"] = ("Find API endpoints, CLI commands, and project entry points", "Architecture"),
        ["get_project_dependencies_all"] = ("List all project-to-project dependency links", "Architecture"),
        ["get_project_dependencies_filtered"] = ("Project dependencies filtered by source/target project", "Architecture"),
        ["get_workspace_content"] = ("Structural breakdown of projects, folders, and files in workspace", "Architecture"),

        ["find_refactor_dead_code"] = ("Detect unreferenced/dead functions, classes, and types", "Refactoring"),
        ["find_refactor_god_objects"] = ("Detect classes with excessive coupling and complexity", "Refactoring"),
        ["analyze_code_impact"] = ("Analyze downstream blast radius / impact of changing a symbol or file", "Refactoring"),
        ["inspect_data_lineage"] = ("Trace database entities, SQL queries, and data lineage", "Refactoring"),

        ["find_symbol_all"] = ("Search for symbols across all kinds (Class, Interface, Function, Struct)", "Symbols"),
        ["find_symbol_class"] = ("Search for classes by name or pattern", "Symbols"),
        ["find_symbol_interface"] = ("Search for interfaces by name or pattern", "Symbols"),
        ["find_symbol_function"] = ("Search for functions and methods by name or pattern", "Symbols"),
        ["get_file_outline"] = ("File outline: types, functions, and symbols declared in file", "Symbols"),
        ["get_call_chain"] = ("Trace caller/callee function invocation chains", "Symbols"),
        ["resolve_call_target"] = ("Find implementation target and definitions for a method call", "Symbols"),

        ["get_taxonomy_nodes"] = ("Counts and distinct labels of all graph nodes", "Taxonomy"),
        ["get_taxonomy_properties"] = ("Property keys and schemas for graph node kinds", "Taxonomy"),
    };

    public static bool Exists(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return ResourceNames.Any(r =>
            r.EndsWith($".Queries.{name}.cypher", StringComparison.OrdinalIgnoreCase) ||
            r.EndsWith($".{name}.cypher", StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<BuiltInQueryInfo> GetBuiltInQueries()
    {
        var names = ResourceNames
            .Where(r => r.EndsWith(".cypher", StringComparison.OrdinalIgnoreCase))
            .Select(r =>
            {
                var idx = r.IndexOf(".Queries.", StringComparison.OrdinalIgnoreCase);
                var sub = idx >= 0 ? r.Substring(idx + ".Queries.".Length) : r;
                return sub.EndsWith(".cypher", StringComparison.OrdinalIgnoreCase) ? sub[..^7] : sub;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        var list = new List<BuiltInQueryInfo>();
        foreach (var n in names)
        {
            if (KnownMetadata.TryGetValue(n, out var meta))
            {
                list.Add(new BuiltInQueryInfo(n, meta.Description, meta.Category));
            }
            else
            {
                list.Add(new BuiltInQueryInfo(n, $"Built-in Cypher query: {n}", "General"));
            }
        }
        return list;
    }
}
