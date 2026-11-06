using System.Collections;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using YamlDotNet.Serialization;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Shared;

/// <summary>
/// Parser for OpenAPI and Swagger contract specifications (.yaml, .yml, .json).
/// Extracts HTTP endpoints, operation IDs, summaries, and component schemas.
/// </summary>
public class OpenApiFileParser : IFileParser
{
    private const string ProtocolRest = "REST";
    private const string OpenApiLanguageName = "openapi";
    private const string YamlExt = ".yaml";
    private const string YmlExt = ".yml";
    private const string JsonExt = ".json";
    private const string OpenApiKeyword = "openapi";
    private const string SwaggerKeyword = "swagger";
    private const string PathsKey = "paths";
    private const string ComponentsKey = "components";
    private const string SchemasKey = "schemas";
    private const string DefinitionsKey = "definitions";
    private const string SummaryKey = "summary";
    private const string DescriptionKey = "description";
    private const string OperationIdKey = "operationId";
    private const string OperationIdSnakeKey = "operation_id";
    private const string TypeKindClass = "class";

    private static readonly HashSet<string> StandardHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS", "TRACE"
    };

    public string LanguageName => OpenApiLanguageName;

    public bool UsesTreeSitter => false;

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } = [];

    public bool CanParse(string fileExtension) => false;

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath) =>
        ImportType.Internal;

    public bool CanParseFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (fileName.Equals("openapi.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("openapi.yml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("openapi.json", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("swagger.yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("swagger.yml", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("swagger.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(filePath);
        if (ext.Equals(YamlExt, StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(YmlExt, StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(JsonExt, StringComparison.OrdinalIgnoreCase))
        {
            var lower = fileName.ToLowerInvariant();
            if (lower.Contains(OpenApiKeyword, StringComparison.Ordinal) || lower.Contains(SwaggerKeyword, StringComparison.Ordinal))
            {
                return true;
            }

            var dirName = Path.GetFileName(Path.GetDirectoryName(filePath));
            if (!string.IsNullOrEmpty(dirName) &&
                (dirName.Equals(OpenApiKeyword, StringComparison.OrdinalIgnoreCase) ||
                 dirName.Equals(SwaggerKeyword, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("OpenApiFileParser parses specifications directly without TreeSitter visitors.");
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var fileId = $"{workspaceId}:{OntologyConstants.IdPrefixes.File}:{relativePath}";
        var fileNode = new FileNode(fileId, fileName, relativePath, filePath);

        var content = await PhysicalFileStore.ConsumeFileTextAsync(filePath);
        var doc = SyntaxDocument.Create(content, LanguageName, filePath, relativePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
        }

        try
        {
            var deserializer = new DeserializerBuilder().Build();
            var rawObj = deserializer.Deserialize(new StringReader(content));
            if (rawObj is IDictionary rootDict)
            {
                ExtractPathsAndEndpoints(rootDict, fileNode, workspaceId, relativePath);
                ExtractComponentSchemas(rootDict, fileNode, workspaceId, relativePath);
            }
        }
        catch (Exception ex)
        {
            // Expected non-fatal parse errors when encountering non-standard or partial OpenAPI documents
            _ = ex.Message;
        }

        return new SyntaxTree(filePath, relativePath, doc, fileNode, this, [], [], []);
    }

    private static void ExtractPathsAndEndpoints(
        IDictionary rootDict,
        FileNode fileNode,
        string workspaceId,
        string relativePath)
    {
        var pathsObj = FindDictValue(rootDict, PathsKey);
        if (pathsObj is not IDictionary pathsDict) return;

        foreach (DictionaryEntry pathEntry in pathsDict)
        {
            var path = pathEntry.Key?.ToString()?.Trim();
            if (string.IsNullOrEmpty(path) || !path.StartsWith('/')) continue;

            if (pathEntry.Value is not IDictionary opDict) continue;

            foreach (DictionaryEntry methodEntry in opDict)
            {
                var method = methodEntry.Key?.ToString()?.Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(method) || !StandardHttpMethods.Contains(method)) continue;

                var opProps = methodEntry.Value as IDictionary;
                var summary = FindDictValue(opProps, SummaryKey)?.ToString()?.Trim();
                var description = FindDictValue(opProps, DescriptionKey)?.ToString()?.Trim();
                var operationId = FindDictValue(opProps, OperationIdKey)?.ToString()?.Trim()
                               ?? FindDictValue(opProps, OperationIdSnakeKey)?.ToString()?.Trim();

                var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{method}:{path}";
                var epName = $"{method} {path}";

                var epNode = new EndpointNode(
                    epId,
                    epName,
                    relativePath,
                    method,
                    path,
                    ProtocolRest
                )
                {
                    FilePath = relativePath,
                    StartLine = 1,
                    EndLine = 1,
                    Line = 1,
                    LineEnd = 1,
                    Operation = method,
                    EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                };

                epNode.CustomProperties ??= new();
                epNode.CustomProperties["path"] = path;
                epNode.CustomProperties["route"] = path;
                epNode.CustomProperties["route_template"] = path;
                epNode.CustomProperties["method"] = method;
                epNode.CustomProperties["http_method"] = method;
                epNode.CustomProperties["file_path"] = relativePath;

                if (!string.IsNullOrEmpty(summary))
                {
                    epNode.CustomProperties["summary"] = summary;
                }
                if (!string.IsNullOrEmpty(operationId))
                {
                    epNode.CustomProperties["operation_id"] = operationId;
                }
                if (!string.IsNullOrEmpty(description))
                {
                    epNode.CustomProperties["description"] = description;
                }

                fileNode.Children.Add(epNode);

                if (!string.IsNullOrEmpty(operationId))
                {
                    var funId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{operationId}";
                    var funcNode = new FunctionNode(
                        funId,
                        operationId,
                        operationId,
                        relativePath,
                        relativePath,
                        1,
                        1,
                        1,
                        1
                    );
                    fileNode.Children.Add(funcNode);
                    fileNode.References.Add(new Reference(epId, funcNode.Id, OntologyConstants.Relationships.ExposedBy));
                }
            }
        }
    }

    private static void ExtractComponentSchemas(
        IDictionary rootDict,
        FileNode fileNode,
        string workspaceId,
        string relativePath)
    {
        IDictionary? schemasDict = null;

        var componentsObj = FindDictValue(rootDict, ComponentsKey);
        if (componentsObj is IDictionary compDict)
        {
            schemasDict = FindDictValue(compDict, SchemasKey) as IDictionary;
        }

        schemasDict ??= FindDictValue(rootDict, DefinitionsKey) as IDictionary;
        if (schemasDict == null) return;

        foreach (DictionaryEntry schemaEntry in schemasDict)
        {
            var schemaName = schemaEntry.Key?.ToString()?.Trim();
            if (string.IsNullOrEmpty(schemaName)) continue;

            var typeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Type:{schemaName}";
            var typeNode = new TypeNode(
                typeId,
                schemaName,
                schemaName,
                relativePath,
                relativePath,
                1,
                1,
                1,
                1,
                TypeKindClass
            );
            fileNode.Children.Add(typeNode);
        }
    }

    private static object? FindDictValue(IDictionary? dict, string key)
    {
        if (dict == null) return null;
        if (dict.Contains(key)) return dict[key];

        foreach (DictionaryEntry entry in dict)
        {
            if (string.Equals(entry.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }
        return null;
    }
}
