using System.Xml.Linq;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.CSharp;

/// <summary>.NET web.config / app.config: connectionStrings, appSettings and WCF client endpoints.</summary>
public sealed class DotNetXmlConfigurationParser : ISharedFileParser
{
    private const int MaxLinkDepth = 3;

    public string Name => "dotnet-xml-configuration";
    public IReadOnlyCollection<string> Extensions { get; } = [".config"];

    public bool CanParse(string fileName) =>
        fileName.Equals("web.config", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("app.config", StringComparison.OrdinalIgnoreCase);

    public IProjectEnrichment? Parse(SharedFileContext file)
    {
        var values = new List<ConfigValue>();
        Collect(XDocument.Parse(file.Content), Path.GetDirectoryName(file.FullPath) ?? "", values, 0);
        return new ConfigurationFileInformation(file.FullPath, "xml", values);
    }

    private static void Collect(XDocument doc, string baseDir, List<ConfigValue> values, int depth)
    {
        if (doc.Root == null) return;

        foreach (var section in Sections(doc, "connectionStrings"))
        {
            FollowLink(section.Attribute("configSource")?.Value ?? section.Attribute("file")?.Value, baseDir, values, depth);

            foreach (var add in Adds(section))
            {
                var name = add.Attribute("name")?.Value?.Trim();
                var connStr = add.Attribute("connectionString")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(connStr))
                {
                    values.Add(new ConfigValue($"ConnectionStrings:{name}", connStr));
                }
            }
        }

        foreach (var section in Sections(doc, "appSettings"))
        {
            FollowLink(section.Attribute("file")?.Value ?? section.Attribute("configSource")?.Value, baseDir, values, depth);

            foreach (var add in Adds(section))
            {
                var key = add.Attribute("key")?.Value?.Trim();
                var val = add.Attribute("value")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(val))
                {
                    values.Add(new ConfigValue(key, val));
                }
            }
        }

        foreach (var client in Sections(doc, "client"))
        {
            foreach (var ep in client.Elements().Where(e => IsNamed(e, "endpoint")))
            {
                var address = ep.Attribute("address")?.Value?.Trim();
                var contract = ep.Attribute("contract")?.Value?.Trim() ?? ep.Attribute("name")?.Value?.Trim() ?? "WcfEndpoint";
                if (!string.IsNullOrWhiteSpace(address))
                {
                    values.Add(new ConfigValue($"WcfClient:{contract}:Address", address));
                }
            }
        }
    }

    private static bool IsNamed(XElement e, string name) => e.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase);
    private static IEnumerable<XElement> Sections(XDocument doc, string name) => doc.Root?.Descendants().Where(e => IsNamed(e, name)) ?? [];
    private static IEnumerable<XElement> Adds(XElement section) => section.Elements().Where(e => IsNamed(e, "add"));

    // Linked files are separate files on disk (configSource / file attributes), resolved relative to the owner.
    private static void FollowLink(string? link, string baseDir, List<ConfigValue> values, int depth)
    {
        if (depth >= MaxLinkDepth || string.IsNullOrWhiteSpace(link)) return;

        var path = ResolveLinkedPath(baseDir, link);
        if (path == null) return;

        try
        {
            Collect(XDocument.Parse(File.ReadAllText(path)), Path.GetDirectoryName(path) ?? "", values, depth + 1);
        }
        catch
        {
            // A broken linked file must not discard the owner's own settings.
        }
    }

    private static string? ResolveLinkedPath(string baseDir, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(baseDir)) return null;

        var normRel = relativePath.Replace('\\', '/');
        var direct = Path.GetFullPath(Path.Combine(baseDir, normRel));
        if (File.Exists(direct)) return direct;

        // A project may reference "../Web.config" which lives in a sibling web project in source control.
        if (!Path.GetFileName(normRel).Equals("web.config", StringComparison.OrdinalIgnoreCase) ||
            !normRel.StartsWith("../", StringComparison.Ordinal))
        {
            return null;
        }

        var parentDir = Path.GetDirectoryName(direct);
        if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir)) return null;

        try
        {
            var siblings = Directory.EnumerateDirectories(parentDir)
                .SelectMany(d => Directory.EnumerateFiles(d, "web.config", SearchOption.TopDirectoryOnly))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return siblings.FirstOrDefault(f =>
                       Path.GetDirectoryName(f) is { } d && (d.EndsWith(".UI", StringComparison.OrdinalIgnoreCase) || d.EndsWith(".Web", StringComparison.OrdinalIgnoreCase)))
                   ?? siblings.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}

