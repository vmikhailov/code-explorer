using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java;

public class JavaFileParser : IFileParser
{
    static JavaFileParser()
    {
        LibraryConfigurationRegistry.Register(new Libraries.SpringFrameworkConfigurationDescriptor());
    }

    public string LanguageName => "java";

    internal static readonly IReadOnlyList<ISemanticExtension> DefaultExtensions =
    [
        new Libraries.DubboLibraryParser(),
        new Libraries.ApacheCamelLibraryParser(),
        new Libraries.ApachePulsarLibraryParser(),
        new Libraries.AxonFrameworkLibraryParser(),
        new Libraries.GrpcJavaLibraryParser(),
        new Libraries.HttpClientJavaLibraryParser(),
        new Libraries.OpenFeignRetrofitLibraryParser(),
        new Libraries.JdbcTemplateLibraryParser(),
        new Libraries.JpaLibraryParser(),
        new Libraries.MyBatisLibraryParser(),
        new Libraries.JdbiLibraryParser(),
        new Libraries.R2dbcLibraryParser(),
        new Libraries.AwsSdkJavaLibraryParser(),
        new Libraries.Resilience4jLibraryParser(),
        new Libraries.TemporalJavaLibraryParser(),
        new Libraries.VertxLibraryParser(),
        new Libraries.AkkaPekkoLibraryParser(),
        new Libraries.SpringIntegrationLibraryParser(),
        new Libraries.SpringBatchLibraryParser(),
        new Libraries.SpringCloudGatewayLibraryParser(),
        new Libraries.DropwizardJerseyLibraryParser(),
        new Libraries.NettyLibraryParser(),
        new Libraries.JooqLibraryParser(),
        new Libraries.RedissonCacheJavaLibraryParser(),
        new Libraries.LangChain4jSpringAiLibraryParser(),
        new Libraries.MicronautLibraryParser(),
        new Libraries.QuarkusJaxRsLibraryParser(),
        new Libraries.QuarkusMessagingLibraryParser(),
        new Libraries.PanacheOrmLibraryParser(),
        new Libraries.SpringDataMongoLibraryParser(),
        new Libraries.SpringEventsLibraryParser(),
        new Libraries.SpringKafkaLibraryParser(),
        new Libraries.SpringMvcLibraryParser(),
        new Libraries.SpringRabbitMqLibraryParser(),
        new Libraries.SpringGraphQlLibraryParser(),
        Libraries.JavaTestLibraryParser.Instance
    ];

    internal static readonly IReadOnlyList<PackageDescriptor> DefaultPackages =
        [.. DefaultExtensions.SelectMany(e => e.GetPackageDescriptors())];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => DefaultExtensions;

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        JavaSyntaxConverter.Instance.Parse(sourceText, LanguageName, filePath, relativePath);

    public LanguageSyntaxProfile SyntaxProfile => JavaSyntaxProfile.Instance;

    public JavaFileParser()
    {
        LibraryConfigurationRegistry.Register(new Libraries.SpringFrameworkConfigurationDescriptor());
    }

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".java", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        JavaDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return new JavaFileVisitor(
            rootNode,
            activeExtensions,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry
        );
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        // Java standard library packages
        if (importPath.StartWithIC("java.") ||
            importPath.StartWithIC("javax.") ||
            importPath.StartWithIC("jakarta.") ||
            importPath.StartWithIC("sun.") ||
            importPath.StartWithIC("com.sun."))
        {
            return ImportType.External;
        }

        if (!string.IsNullOrEmpty(absoluteWorkspacePath))
        {
            // Check if matches any internal workspace source package directory
            var importRelDir = importPath.Replace('.', '/');
            if (importRelDir.Contains('/'))
            {
                var dirPart = importRelDir[..importRelDir.LastIndexOf('/')];
                var possiblePath = Path.Combine(absoluteWorkspacePath, "src/main/java", dirPart);
                if (Directory.Exists(possiblePath)) return ImportType.Internal;

                possiblePath = Path.Combine(absoluteWorkspacePath, "src", dirPart);
                if (Directory.Exists(possiblePath)) return ImportType.Internal;
            }
        }

        return ImportType.External;
    }
}
