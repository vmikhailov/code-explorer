using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Kotlin;

public class KotlinFileParser : IFileParser
{
    public string LanguageName => "kotlin";

    public bool UsesTreeSitter => false;

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".kt", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".kts", StringComparison.OrdinalIgnoreCase);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("Kotlin parser uses high-resilience native C# token analysis.");
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        if (importPath.StartsWith("kotlin.", StringComparison.OrdinalIgnoreCase) ||
            importPath.StartsWith("kotlinx.", StringComparison.OrdinalIgnoreCase) ||
            importPath.StartsWith("java.", StringComparison.OrdinalIgnoreCase) ||
            importPath.StartsWith("javax.", StringComparison.OrdinalIgnoreCase) ||
            importPath.StartsWith("android.", StringComparison.OrdinalIgnoreCase) ||
            importPath.StartsWith("androidx.", StringComparison.OrdinalIgnoreCase) ||
            importPath.StartsWith("io.flutter.", StringComparison.OrdinalIgnoreCase))
        {
            return ImportType.External;
        }

        if (!string.IsNullOrEmpty(absoluteWorkspacePath))
        {
            var rel = importPath.Replace('.', '/');
            if (rel.Contains('/'))
            {
                var dirPart = rel[..rel.LastIndexOf('/')];
                if (Directory.Exists(Path.Combine(absoluteWorkspacePath, "src/main/kotlin", dirPart)) ||
                    Directory.Exists(Path.Combine(absoluteWorkspacePath, "src/main/java", dirPart)) ||
                    Directory.Exists(Path.Combine(absoluteWorkspacePath, "src", dirPart)))
                {
                    return ImportType.Internal;
                }
            }
        }

        return ImportType.External;
    }

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

        var rawImports = new List<RawImport>();
        var rawVariables = new List<RawVariable>();
        var rawTypeBindings = new List<RawTypeBinding>();

        // 1. Package declaration
        string? packageName = null;
        var pkgMatch = Regex.Match(content, @"^\s*package\s+([a-zA-Z0-9_.]+)", RegexOptions.Multiline);
        if (pkgMatch.Success)
        {
            packageName = pkgMatch.Groups[1].Value.Trim();
        }

        // 2. Imports
        var importMatches = Regex.Matches(content, @"^\s*import\s+([a-zA-Z0-9_.*]+)", RegexOptions.Multiline);
        foreach (Match m in importMatches)
        {
            var imp = m.Groups[1].Value.Trim();
            rawImports.Add(new RawImport(imp, filePath));
        }

        // 3. String constant channel names: val installChannel = "hearai/apk_install"
        var stringVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var valMatches = Regex.Matches(content, @"(?:val|var)\s+([a-zA-Z0-9_]+)\s*=\s*['""]([^'""]+)['""]");
        foreach (Match m in valMatches)
        {
            var varName = m.Groups[1].Value.Trim();
            var varVal = m.Groups[2].Value.Trim();
            stringVars[varName] = varVal;
            rawVariables.Add(new RawVariable(varName, varVal, "global", true, filePath, 1, 1, 0, 0));
        }

        // 4. Classes / Interfaces / Objects
        var classRegex = new Regex(@"(?:data\s+|sealed\s+|enum\s+|abstract\s+|open\s+|inner\s+)?(class|interface|object)\s+([a-zA-Z0-9_]+)(?:<[^>]+>)?(?:\s*\([^)]*\))?(?:\s*:\s*([^{]+))?", RegexOptions.Multiline);
        var classMatches = classRegex.Matches(content);

        TypeNode? primaryClassNode = null;

        foreach (Match cm in classMatches)
        {
            var kind = cm.Groups[1].Value.ToLowerInvariant();
            var className = cm.Groups[2].Value.Trim();
            var inheritancePart = cm.Groups[3].Success ? cm.Groups[3].Value.Trim() : null;

            var typeId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Type:{className}";
            var extensions = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(packageName))
            {
                extensions["namespace"] = packageName;
            }

            if (!string.IsNullOrEmpty(inheritancePart))
            {
                var cleanBase = Regex.Replace(inheritancePart, @"\(.*?\)", "").Trim();
                extensions["extends"] = cleanBase;
            }

            var typeNode = new TypeNode(
                typeId,
                className,
                className,
                relativePath,
                relativePath,
                1,
                1,
                0,
                0,
                kind,
                extensions
            );

            fileNode.Children.Add(typeNode);
            primaryClassNode ??= typeNode;
        }

        // 5. Functions / Methods
        var funRegex = new Regex(@"(?:override\s+|private\s+|protected\s+|public\s+|internal\s+|suspend\s+|inline\s+)*fun\s+(?:<[^>]+>\s+)?(?:([a-zA-Z0-9_]+)\.)?([a-zA-Z0-9_]+)\s*\(([^)]*)\)", RegexOptions.Multiline);
        var funMatches = funRegex.Matches(content);

        foreach (Match fm in funMatches)
        {
            var funName = fm.Groups[2].Value.Trim();
            var receiver = fm.Groups[1].Success ? fm.Groups[1].Value.Trim() : null;
            var funId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{funName}";

            var extensions = new Dictionary<string, string>();
            if (receiver != null) extensions["receiver"] = receiver;

            var funcNode = new FunctionNode(
                funId,
                funName,
                funName,
                relativePath,
                relativePath,
                1,
                1,
                0,
                0,
                extensions
            );

            if (primaryClassNode != null)
            {
                primaryClassNode.Children.Add(funcNode);
            }
            else
            {
                fileNode.Children.Add(funcNode);
            }
        }

        // 6. Flutter MethodChannels: MethodChannel(messenger, "channel_name") or MethodChannel(messenger, installChannel)
        var channelRegex = new Regex(@"MethodChannel\s*\(\s*[^,]+,\s*(?:['""]([^'""]+)['""]|([a-zA-Z0-9_]+))\s*\)");
        var channelMatches = channelRegex.Matches(content);

        var registeredChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match chm in channelMatches)
        {
            var literal = chm.Groups[1].Success ? chm.Groups[1].Value.Trim() : null;
            var idVar = chm.Groups[2].Success ? chm.Groups[2].Value.Trim() : null;

            var channelName = literal;
            if (string.IsNullOrEmpty(channelName) && !string.IsNullOrEmpty(idVar) && stringVars.TryGetValue(idVar, out var resolved))
            {
                channelName = resolved;
            }

            if (!string.IsNullOrEmpty(channelName) && registeredChannels.Add(channelName))
            {
                var channelId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:channel:{channelName.ToLowerInvariant()}";
                var channelEp = new EntryPointNode(
                    channelId,
                    $"MethodChannel {channelName}",
                    relativePath,
                    "channel",
                    new()
                    {
                        ["channel_name"] = channelName,
                        ["platform"] = "flutter"
                    }
                );

                fileNode.Children.Add(channelEp);
            }
        }

        foreach (var (varName, varVal) in stringVars)
        {
            if (varName.EndsWith("Channel", StringComparison.OrdinalIgnoreCase) && varVal.Contains('/') && registeredChannels.Add(varVal))
            {
                var channelId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:channel:{varVal.ToLowerInvariant()}";
                var channelEp = new EntryPointNode(
                    channelId,
                    $"MethodChannel {varVal}",
                    relativePath,
                    "channel",
                    new()
                    {
                        ["channel_name"] = varVal,
                        ["platform"] = "flutter"
                    }
                );
                fileNode.Children.Add(channelEp);
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
            rawImports,
            rawVariables,
            rawTypeBindings
        );
    }
}
