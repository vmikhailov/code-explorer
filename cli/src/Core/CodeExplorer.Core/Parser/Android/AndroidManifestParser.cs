using System.Xml.Linq;

namespace CodeExplorer.Core.Parser.Android;

public static class AndroidManifestParser
{
    private static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";

    public static AndroidManifestInfo? ParseFile(string manifestPath)
    {
        if (!File.Exists(manifestPath)) return null;

        try
        {
            var content = File.ReadAllText(manifestPath);
            return ParseContent(content, Path.GetDirectoryName(manifestPath));
        }
        catch
        {
            return null;
        }
    }

    public static AndroidManifestInfo? ParseContent(string xmlContent, string? directoryContext = null)
    {
        if (string.IsNullOrWhiteSpace(xmlContent)) return null;

        try
        {
            var doc = XDocument.Parse(xmlContent);
            var root = doc.Root;
            if (root == null) return null;

            var packageName = root.Attribute("package")?.Value?.Trim();

            var permissions = new List<string>();
            foreach (var permElem in root.Elements("uses-permission"))
            {
                var name = permElem.Attribute(AndroidNs + "name")?.Value?.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    permissions.Add(name);
                }
            }

            var appElem = root.Element("application");
            string? appLabel = null;
            var activities = new List<AndroidActivityInfo>();
            var deepLinks = new List<AndroidDeepLinkInfo>();
            var services = new List<string>();
            var providers = new List<string>();
            var receivers = new List<string>();

            if (appElem != null)
            {
                var rawLabel = appElem.Attribute(AndroidNs + "label")?.Value?.Trim();
                appLabel = ResolveStringResource(rawLabel, directoryContext);

                foreach (var actElem in appElem.Elements("activity"))
                {
                    var actName = actElem.Attribute(AndroidNs + "name")?.Value?.Trim() ?? "";
                    var exportedAttr = actElem.Attribute(AndroidNs + "exported")?.Value?.Trim();
                    var isExported = string.Equals(exportedAttr, "true", StringComparison.OrdinalIgnoreCase);
                    var isLauncher = false;

                    foreach (var filterElem in actElem.Elements("intent-filter"))
                    {
                        var actions = filterElem.Elements("action")
                            .Select(a => a.Attribute(AndroidNs + "name")?.Value?.Trim())
                            .Where(a => !string.IsNullOrEmpty(a))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        var categories = filterElem.Elements("category")
                            .Select(c => c.Attribute(AndroidNs + "name")?.Value?.Trim())
                            .Where(c => !string.IsNullOrEmpty(c))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        if (actions.Contains("android.intent.action.MAIN") &&
                            categories.Contains("android.intent.category.LAUNCHER"))
                        {
                            isLauncher = true;
                        }

                        if (actions.Contains("android.intent.action.VIEW"))
                        {
                            foreach (var dataElem in filterElem.Elements("data"))
                            {
                                var scheme = dataElem.Attribute(AndroidNs + "scheme")?.Value?.Trim();
                                if (!string.IsNullOrEmpty(scheme))
                                {
                                    var host = dataElem.Attribute(AndroidNs + "host")?.Value?.Trim();
                                    var path = dataElem.Attribute(AndroidNs + "path")?.Value?.Trim()
                                               ?? dataElem.Attribute(AndroidNs + "pathPrefix")?.Value?.Trim()
                                               ?? dataElem.Attribute(AndroidNs + "pathPattern")?.Value?.Trim();

                                    deepLinks.Add(new AndroidDeepLinkInfo(
                                        Scheme: scheme,
                                        Host: host,
                                        Path: path,
                                        Action: "android.intent.action.VIEW",
                                        ActivityName: actName
                                    ));
                                }
                            }
                        }
                    }

                    activities.Add(new AndroidActivityInfo(actName, isLauncher, isExported));
                }

                foreach (var srvElem in appElem.Elements("service"))
                {
                    var srvName = srvElem.Attribute(AndroidNs + "name")?.Value?.Trim();
                    if (!string.IsNullOrEmpty(srvName)) services.Add(srvName);
                }

                foreach (var provElem in appElem.Elements("provider"))
                {
                    var provName = provElem.Attribute(AndroidNs + "name")?.Value?.Trim();
                    if (!string.IsNullOrEmpty(provName)) providers.Add(provName);
                }

                foreach (var recElem in appElem.Elements("receiver"))
                {
                    var recName = recElem.Attribute(AndroidNs + "name")?.Value?.Trim();
                    if (!string.IsNullOrEmpty(recName)) receivers.Add(recName);
                }
            }

            return new AndroidManifestInfo(
                PackageName: packageName,
                AppLabel: appLabel,
                Permissions: permissions,
                Activities: activities,
                DeepLinks: deepLinks,
                Services: services,
                Providers: providers,
                Receivers: receivers
            );
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveStringResource(string? rawLabel, string? directoryContext)
    {
        if (string.IsNullOrWhiteSpace(rawLabel)) return null;

        if (!rawLabel.StartsWith("@string/", StringComparison.OrdinalIgnoreCase))
        {
            return rawLabel;
        }

        var key = rawLabel["@string/".Length..].Trim();
        if (string.IsNullOrEmpty(directoryContext) || !Directory.Exists(directoryContext))
        {
            return key;
        }

        var candidateFiles = new[]
        {
            Path.Combine(directoryContext, "res", "values", "strings.xml"),
            Path.Combine(directoryContext, "src", "main", "res", "values", "strings.xml"),
            Path.Combine(directoryContext, "..", "res", "values", "strings.xml"),
            Path.Combine(directoryContext, "..", "src", "main", "res", "values", "strings.xml")
        };

        foreach (var path in candidateFiles)
        {
            try
            {
                if (File.Exists(path))
                {
                    var doc = XDocument.Load(path);
                    var match = doc.Root?.Elements("string")
                        .FirstOrDefault(s => string.Equals(s.Attribute("name")?.Value, key, StringComparison.OrdinalIgnoreCase));
                    if (match != null && !string.IsNullOrWhiteSpace(match.Value))
                    {
                        return match.Value.Trim();
                    }
                }
            }
            catch
            {
                // Fallback
            }
        }

        return key;
    }
}
