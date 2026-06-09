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

}
