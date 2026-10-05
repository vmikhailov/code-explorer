using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using TreeSitter;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Android;

public class AndroidManifestFileParser : IFileParser
{
    public string LanguageName => "android-manifest";

    public bool UsesTreeSitter => false;

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    public bool CanParse(string fileExtension) => false;

    public bool CanParseFile(string filePath) =>
        Path.GetFileName(filePath).Equals("AndroidManifest.xml", StringComparison.OrdinalIgnoreCase);

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("AndroidManifestFileParser uses XML parsing directly.");
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath) =>
        ImportType.External;

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var fileNodeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";

        var fileNode = new FileNode(fileNodeId, fileName, relativePath, filePath);
        var content = await File.ReadAllTextAsync(filePath);
        var manifest = AndroidManifestParser.ParseContent(content, Path.GetDirectoryName(filePath));

        if (manifest != null)
        {
            // 1. Launcher Activity as EntryPointNode
            var launcherActivity = manifest.Activities.FirstOrDefault(a => a.IsLauncher);
            if (launcherActivity != null)
            {
                var actClean = launcherActivity.Name.TrimStart('.');
                var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:activity:{actClean.ToLowerInvariant()}";
                var ep = new EntryPointNode(
                    epId,
                    $"Activity {launcherActivity.Name}",
                    relativePath,
                    "activity",
                    new()
                    {
                        ["activity_name"] = launcherActivity.Name,
                        ["is_launcher"] = "true",
                        ["exported"] = launcherActivity.Exported ? "true" : "false"
                    }
                );
                fileNode.Children.Add(ep);
            }

            // 2. Deep links as EndpointNode
            var registeredDeepLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dl in manifest.DeepLinks)
            {
                var hostPart = !string.IsNullOrEmpty(dl.Host) ? dl.Host : "*";
                var pathPart = !string.IsNullOrEmpty(dl.Path) ? dl.Path : "";
                var routeTemplate = $"{dl.Scheme}://{hostPart}{pathPart}";

                if (registeredDeepLinks.Add(routeTemplate))
                {
                    var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:deeplink:{dl.Scheme}:{hostPart}:{pathPart}".ToLowerInvariant();
                    var ep = new EndpointNode(
                        epId,
                        $"DeepLink {routeTemplate}",
                        relativePath,
                        "VIEW",
                        routeTemplate,
                        "DeepLink",
                        true,
                        null,
                        null,
                        null,
                        null,
                        null,
                        new()
                        {
                            ["scheme"] = dl.Scheme,
                            ["activity"] = dl.ActivityName
                        }
                    );
                    fileNode.Children.Add(ep);
                }
            }

            // 3. Providers as EntryPointNode
            foreach (var prov in manifest.Providers)
            {
                var shortName = prov.Contains('.') ? prov.Split('.')[^1] : prov;
                var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:provider:{shortName.ToLowerInvariant()}";
                var ep = new EntryPointNode(
                    epId,
                    $"Provider {shortName}",
                    relativePath,
                    "provider",
                    new()
                    {
                        ["provider_class"] = prov
                    }
                );
                fileNode.Children.Add(ep);
            }
        }

        return new SyntaxTree(
            filePath,
            relativePath,
            null,
            null,
            null,
            fileNode,
            this,
            [],
            [],
            []
        );
    }
}
