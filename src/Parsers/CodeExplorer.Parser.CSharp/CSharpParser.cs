using System.Collections.Concurrent;
using CodeExplorer.Core.Parser;
using TreeSitter;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.CSharp;

public class CSharpParser : IProjectParser, IFileParser
{
    public string LanguageName => "c-sharp";

    public string ProjectType => "csharp";

    public IReadOnlyCollection<string> ExcludedFolders => ["bin", "obj", ".vs"];

    public IReadOnlyList<ILibraryParser> LibraryParsers { get; } =
    [
        new Libraries.CouchbaseLibraryParser(),
        new Libraries.DapperLibraryParser(),
        new Libraries.EfCoreLibraryParser(),
        new Libraries.ElasticsearchNetLibraryParser(),
        new Libraries.FlurlLibraryParser(),
        new Libraries.HttpClientLibraryParser(),
        new Libraries.MicrosoftDataSqlClientLibraryParser(),
        new Libraries.MongoDbCsLibraryParser(),
        new Libraries.MySqlDataLibraryParser(),
        new Libraries.NestLibraryParser(),
        new Libraries.NpgsqlLibraryParser(),
        new Libraries.OracleDataAccessLibraryParser(),
        new Libraries.StackExchangeRedisLibraryParser(),
        new Libraries.Neo4jDriverLibraryParser(),
        new Libraries.AspNetCoreLibraryParser(),
        new Libraries.MassTransitLibraryParser(),
        new Libraries.MediatRLibraryParser(),

        // Generic Cloud Services
        new GenericLibraryParser("stripe", "Stripe", "cloud", ["stripe", "Stripe"]),
        new GenericLibraryParser("aws", "AWS", "cloud", ["Amazon.S3", "AWSSDK"]),
        new GenericLibraryParser("gcp", "GCP", "cloud", ["Google.Cloud."]),
        new GenericLibraryParser("azure", "Azure", "cloud", ["Azure."]),

        // Generic API Clients
        new Libraries.RestSharpLibraryParser(),
        new GenericLibraryParser("refit", "Refit", "api", ["Refit"]),
        new GenericLibraryParser("webapiclient", "WebApiClient", "api", ["WebApiClient"]),
        new GenericLibraryParser("apizr", "Apizr", "api", ["Apizr"]),
        new GenericLibraryParser("notoriousclient", "NotoriousClient", "api", ["NotoriousClient"]),
    ];

    public CSharpParser()
    {
    }


    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".cs", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".csproj")
            {
                return true;
            }
        }
        return false;
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry)
    {
        return new CSharpFileVisitor(
            rootNode,
            activeLibraryParsers,
            this,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            libraryRegistry
        );
    }

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var csprojFiles = Directory.GetFiles(projectDirectory, "*.csproj");
        if (csprojFiles.Length == 0) return null;

        var csprojFile = csprojFiles[0];
        try
        {
            var content = await File.ReadAllTextAsync(csprojFile);
            var doc = System.Xml.Linq.XDocument.Parse(content);

            // Check IsPackable
            var isPackableStr = doc.Descendants("IsPackable").FirstOrDefault()?.Value;
            if (!string.IsNullOrEmpty(isPackableStr) && bool.TryParse(isPackableStr, out var isPackable) && !isPackable)
            {
                return null;
            }

            // Check OutputType (if Exe and not packable, return null)
            var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
            var hasGeneratePackageOnBuild = doc.Descendants("GeneratePackageOnBuild").FirstOrDefault()?.Value;
            var generateOnBuild = !string.IsNullOrEmpty(hasGeneratePackageOnBuild) &&
                                  bool.TryParse(hasGeneratePackageOnBuild, out var gen) && gen;

            var explicitPackable = !string.IsNullOrEmpty(isPackableStr) &&
                                   bool.TryParse(isPackableStr, out var p) && p;

            if (string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase) && !generateOnBuild && !explicitPackable)
            {
                return null;
            }

            var packageId = doc.Descendants("PackageId").FirstOrDefault()?.Value
                         ?? doc.Descendants("AssemblyName").FirstOrDefault()?.Value
                         ?? Path.GetFileNameWithoutExtension(csprojFile);

            var version = doc.Descendants("Version").FirstOrDefault()?.Value
                       ?? doc.Descendants("PackageVersion").FirstOrDefault()?.Value
                       ?? "1.0.0";

            return new ProducedPackageInfo(packageId, version, "nuget");
        }
        catch
        {
            return null;
        }
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        var csprojFiles = Directory.GetFiles(projectDirectory, "*.csproj");
        foreach (var csprojFile in csprojFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(csprojFile);
                var doc = System.Xml.Linq.XDocument.Parse(content);

                // Extract local project references
                var projectRefs = doc.Descendants("ProjectReference");
                foreach (var pref in projectRefs)
                {
                    var include = pref.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(include)) continue;

                    var referencedCsprojPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(csprojFile)!, include)).Replace('\\', '/');
                    var referencedProjectDir = Path.GetFullPath(Path.GetDirectoryName(referencedCsprojPath)!).Replace('\\', '/');
                    localProjectPaths.Add(referencedProjectDir);
                }

                // Extract NuGet package references
                var packageRefs = doc.Descendants("PackageReference");
                foreach (var packRef in packageRefs)
                {
                    var name = packRef.Attribute("Include")?.Value;
                    var version = packRef.Attribute("Version")?.Value ?? packRef.Element("Version")?.Value ?? "unknown";
                    if (string.IsNullOrEmpty(name)) continue;

                    externalPackages.Add(new ProducedPackageInfo(name, version, "nuget"));
                }
            }
            catch
            {
                // Ignore
            }
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    public bool UsesTreeSitter => true;
    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(LibraryParsers, syntaxTree);

    private readonly ConcurrentDictionary<string, string> _csProjCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string?> _dirToCsprojCache = new(StringComparer.OrdinalIgnoreCase);

    public ImportType ResolveCsImportType(string importPath, string filePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        var dir = Path.GetDirectoryName(filePath);
        var csprojFile = FindCsprojFile(dir);
        if (csprojFile != null)
        {
            var rootNamespace = _csProjCache.GetOrAdd(csprojFile, f => Path.GetFileNameWithoutExtension(f));
            var rootPrefix = rootNamespace.Split('.')[0]; // e.g. "CodeExplorer" from "CodeExplorer.Core"

            if (importPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ||
                importPath.StartsWith(rootNamespace, StringComparison.OrdinalIgnoreCase))
            {
                return ImportType.Internal;
            }
        }

        // Standard built-in .NET namespaces
        var builtInPrefixes = new[] { "System", "Microsoft.Win32", "Microsoft.CSharp", "Microsoft.VisualBasic" };
        foreach (var prefix in builtInPrefixes)
        {
            if (importPath.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                importPath.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
            {
                return ImportType.External;
            }
        }

        return ImportType.External;
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ResolveCsImportType(importPath, filePath);
    }

    private string? FindCsprojFile(string? dir)
    {
        if (dir == null) return null;
        return _dirToCsprojCache.GetOrAdd(dir, d =>
        {
            var current = d;
            while (current != null && Directory.Exists(current))
            {
                var files = Directory.GetFiles(current, "*.csproj");
                if (files.Length > 0)
                    return files[0];
                current = Path.GetDirectoryName(current);
            }
            return null;
        });
    }

    public void CollectSemanticData(Node node, string filePath, List<RawImport> rawImports, List<RawVariable> rawVariables)
    {
        if (node.Is(TreeSitterSyntax.CSharp.UsingDirective))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name)
                ?? node.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.QualifiedName, TreeSitterSyntax.Common.Identifier));
            if (nameNode.IsValid())
            {
                var importPath = nameNode.Text;
                var type = ResolveCsImportType(importPath, filePath);
                rawImports.Add(new RawImport(importPath, filePath, type));
            }
        }
        else if (node.IsAny(TreeSitterSyntax.CSharp.VariableDeclarator, TreeSitterSyntax.CSharp.PropertyDeclaration))
        {
            var name = node.GetField(TreeSitterSyntax.Fields.Name)?.Text;
            if (string.IsNullOrEmpty(name))
            {
                name = node.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text;
            }

            if (!string.IsNullOrEmpty(name))
            {
                var valueNode = node.GetField(TreeSitterSyntax.Fields.Value);
                if (!valueNode.IsValid())
                {
                    var eqClause = node.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                    if (eqClause.IsValid() && eqClause.Children.Count > 1)
                    {
                        valueNode = eqClause.Children[1];
                    }
                }
                var initializerText = valueNode.IsValid() ? valueNode.Text : "";
                var isConstant = IsCSharpConstant(node);
                var scope = DetermineCSharpScope(node);

                rawVariables.Add(new RawVariable(
                    name,
                    initializerText,
                    scope,
                    isConstant,
                    filePath,
                    node.StartPosition.Row,
                    node.EndPosition.Row,
                    node.StartPosition.Column,
                    node.EndPosition.Column
                ));
            }
        }
    }

    private static bool IsCSharpConstant(Node node)
    {
        var curr = node;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.CSharp.FieldDeclaration, TreeSitterSyntax.CSharp.LocalDeclarationStatement))
            {
                foreach (var child in curr.Children)
                {
                    if (child.IsAny(TreeSitterSyntax.CSharp.Const, TreeSitterSyntax.CSharp.Readonly) || child.Text is "const" or "readonly")
                        return true;
                }
            }
            curr = curr.Parent;
        }
        return false;
    }

    private static string DetermineCSharpScope(Node node)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration, TreeSitterSyntax.CSharp.InterfaceDeclaration))
                return "class";
            if (curr.IsAny(TreeSitterSyntax.CSharp.MethodDeclaration, TreeSitterSyntax.CSharp.LocalFunctionStatement, TreeSitterSyntax.CSharp.Block, TreeSitterSyntax.CSharp.ConstructorDeclaration))
                return "local";
            curr = curr.Parent;
        }
        return "global";
    }
}
