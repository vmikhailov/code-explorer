using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp;

public class CSharpFileParser : IFileParser
{
    public string LanguageName => "c-sharp";

    private static readonly Regex CSharpDbContextClassRegex = new(@"class\s+([A-Za-z0-9_]+)\s*:\s*(?:[A-Za-z0-9_.]+\.)?(?:Identity)?DbContext\b", RegexOptions.Compiled);
    private static readonly Regex CSharpBaseConnectionNameRegex = new(@":\s*base\s*\(\s*@?[""'](?:name\s*=\s*)?([A-Za-z0-9_.\-:]+)[""']\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public void EnrichFileSignals(
        CodeExplorer.Core.Common.Nodes.Layer1_Physical.FileNode fileNode,
        SyntaxTree syntaxTree,
        CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode? project,
        string sourceText)
    {
        if (sourceText.Contains("DbContext", StringComparison.Ordinal))
        {
            EnrichDbContextSignals(fileNode, sourceText);
            if (!ReferenceEquals(fileNode, syntaxTree.FileNode))
            {
                EnrichDbContextSignals(syntaxTree.FileNode, sourceText);
            }
        }
    }

    private static void EnrichDbContextSignals(CodeExplorer.Core.Common.Nodes.Layer1_Physical.FileNode targetFile, string sourceText)
    {
        var signals = CodeExplorer.Core.Common.Nodes.AnalysisSignalStore.Signals(targetFile);
        var baseMatch = CSharpBaseConnectionNameRegex.Match(sourceText);
        if (baseMatch.Success)
        {
            signals.DbContextBaseConn = baseMatch.Groups[1].Value.Trim();
        }

        var ctxMatches = CSharpDbContextClassRegex.Matches(sourceText);
        if (ctxMatches.Count > 0)
        {
            var classes = new List<string>(ctxMatches.Count);
            foreach (Match m in ctxMatches)
            {
                var className = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(className) &&
                    !className.Equals("DbContext", StringComparison.OrdinalIgnoreCase) &&
                    !className.Equals("IdentityDbContext", StringComparison.OrdinalIgnoreCase))
                {
                    classes.Add(className);
                }
            }
            if (classes.Count > 0)
            {
                signals.DbContextClasses = string.Join(",", classes);
            }
        }
    }

    internal static readonly IReadOnlyList<ISemanticExtension> DefaultExtensions =
    [
        new Libraries.CouchbaseLibraryParser(),
        new Libraries.DapperLibraryParser(),
        new Libraries.EfCoreLibraryParser(),
        new Libraries.ElasticsearchNetLibraryParser(),
        new Libraries.FlurlLibraryParser(),
        new Libraries.HttpClientLibraryParser(),
        new Libraries.MicrosoftDataSqlClientLibraryParser(),
        new Libraries.SystemDataSqlClientLibraryParser(),
        new Libraries.MongoDbCsLibraryParser(),
        new Libraries.MySqlDataLibraryParser(),
        new Libraries.NestLibraryParser(),
        new Libraries.NpgsqlLibraryParser(),
        new Libraries.OracleDataAccessLibraryParser(),
        new Libraries.StackExchangeRedisLibraryParser(),
        new Libraries.Neo4jDriverLibraryParser(),
        new Libraries.AspNetCoreLibraryParser(),
        new Libraries.AzureFunctionsLibraryParser(),
        new Libraries.HotChocolateLibraryParser(),
        new Libraries.GrpcCSharpLibraryParser(),
        new Libraries.WcfLibraryParser(),
        new Libraries.MassTransitLibraryParser(),
        new Libraries.MassTransitSagaLibraryParser(),
        new Libraries.HangfireLibraryParser(),
        new Libraries.QuartzNetLibraryParser(),
        new Libraries.WolverineLibraryParser(),
        new Libraries.CapLibraryParser(),
        new Libraries.EventBusLibraryParser(),
        new Libraries.MediatRLibraryParser(),
        new Libraries.KafkaFlowLibraryParser(),
        new Libraries.ConfluentKafkaLibraryParser(),
        new Libraries.OrleansLibraryParser(),
        new Libraries.RestSharpLibraryParser(),
        new Libraries.RefitLibraryParser(),
        new Libraries.StripeLibraryParser(),
        new Libraries.MartenLibraryParser(),
        new Libraries.AwsSdkDotNetLibraryParser(),
        new Libraries.AzureSdkDotNetLibraryParser(),
        new Libraries.PollyResilienceLibraryParser(),
        new Libraries.OpenTelemetryDotNetLibraryParser(),
        new Libraries.NServiceBusLibraryParser(),
        new Libraries.RebusLibraryParser(),
        new Libraries.DaprDotNetLibraryParser(),
        new Libraries.FastEndpointsLibraryParser(),
        new Libraries.CarterLibraryParser(),
        new Libraries.YarpReverseProxyLibraryParser(),
        new Libraries.Linq2DbNHibernateLibraryParser(),
        new Libraries.SemanticKernelAiDotNetLibraryParser(),
        Libraries.DotNetTestLibraryParser.Instance
    ];

    internal static readonly IReadOnlyList<PackageDescriptor> DefaultPackages =
        [.. DefaultExtensions.SelectMany(e => e.GetPackageDescriptors())];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => DefaultExtensions;

    public LanguageSyntaxProfile SyntaxProfile => CSharpSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".cs", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        CSharpDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return new CSharpFileVisitor(
            rootNode,
            activeExtensions,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry
        );
    }

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        CSharpSyntaxConverter.Instance.Parse(sourceText, LanguageName, filePath, relativePath);

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

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

            if (importPath.StartWithIC(rootPrefix) ||
                importPath.StartWithIC(rootNamespace))
            {
                return ImportType.Internal;
            }
        }

        // Standard built-in .NET namespaces
        var builtInPrefixes = new[] { "System", "Microsoft.Win32", "Microsoft.CSharp", "Microsoft.VisualBasic" };
        foreach (var prefix in builtInPrefixes)
        {
            if (importPath.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                importPath.StartWithIC(prefix + "."))
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
                var initializerText = valueNode.IsValid() ? valueNode.GetBoundedInitializerText() : "";
                var isConstant = IsCSharpConstant(node);
                var scope = DetermineCSharpScope(node);

                string? memberKind = null;
                string? typeName = null;

                if (node.Is(TreeSitterSyntax.CSharp.PropertyDeclaration))
                {
                    memberKind = "property";
                    var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
                    if (typeNode.IsValid())
                    {
                        typeName = typeNode.Text;
                    }
                }
                else if (node.Is(TreeSitterSyntax.CSharp.VariableDeclarator))
                {
                    var parent = node.Parent;
                    if (parent.IsValid() && parent.Is(TreeSitterSyntax.CSharp.VariableDeclaration))
                    {
                        var typeNode = parent.GetField(TreeSitterSyntax.Fields.Type);
                        if (typeNode.IsValid())
                        {
                            typeName = typeNode.Text;
                        }
                        if (parent.Parent.IsValid() && parent.Parent.Is(TreeSitterSyntax.CSharp.FieldDeclaration))
                        {
                            memberKind = "field";
                        }
                        else
                        {
                            memberKind = "variable";
                        }
                    }
                }

                rawVariables.Add(new RawVariable(
                    name,
                    initializerText,
                    scope,
                    isConstant,
                    filePath,
                    node.StartPosition.Row + 1,
                    node.EndPosition.Row + 1,
                    node.StartPosition.Column + 1,
                    node.EndPosition.Column + 1,
                    typeName,
                    memberKind
                ));
            }
        }
        else if (node.Is(TreeSitterSyntax.CSharp.Parameter))
        {
            var parent = node.Parent;
            if (parent.IsValid() && parent.Is(TreeSitterSyntax.CSharp.ParameterList))
            {
                var recordNode = parent.Parent;
                if (recordNode.IsValid() && recordNode.IsAny(
                    TreeSitterSyntax.CSharp.RecordDeclaration,
                    TreeSitterSyntax.CSharp.ClassDeclaration,
                    TreeSitterSyntax.CSharp.StructDeclaration))
                {
                    var name = node.GetField(TreeSitterSyntax.Fields.Name)?.Text
                        ?? node.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text;
                    if (!string.IsNullOrEmpty(name))
                    {
                        var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
                        var typeName = typeNode.IsValid() ? typeNode.Text : null;
                        var scope = DetermineCSharpScope(recordNode);
                        rawVariables.Add(new RawVariable(
                            name,
                            "",
                            scope,
                            false,
                            filePath,
                            node.StartPosition.Row + 1,
                            node.EndPosition.Row + 1,
                            node.StartPosition.Column + 1,
                            node.EndPosition.Column + 1,
                            typeName,
                            "property"
                        ));
                    }
                }
            }
        }
    }

    private static bool IsCSharpConstant(Node node)
    {
        var curr = node;
        while (curr.IsValid())
        {
            if (curr is VariableDeclSyntax { IsConstant: true } ||
                (curr is DeclarationSyntax d && (d.Modifiers.HasFlag(SyntaxModifiers.Const) || d.Modifiers.HasFlag(SyntaxModifiers.ReadOnly))))
                return true;
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
