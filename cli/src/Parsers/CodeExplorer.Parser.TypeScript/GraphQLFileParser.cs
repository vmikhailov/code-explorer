using System.Text;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript;

public sealed record GraphQLParsedOperation(
    string OperationType,
    string? OperationName,
    IReadOnlyList<string> RootFields,
    int StartLine,
    int EndLine);

public sealed record GraphQLParsedSdlField(
    string OperationType,
    string FieldName,
    string? ReturnType,
    int Line);

/// <summary>
/// Dedicated file parser for GraphQL documents (.graphql, .gql).
/// Extracts executable operations (query, mutation, subscription) with their top-level
/// root field selections (including aliased and multi-root-field operations) as well as
/// SDL root type definitions (type Query, type Mutation, type Subscription).
/// </summary>
public class GraphQLFileParser : IFileParser
{
    public string LanguageName => "graphql";
    public bool UsesTreeSitter => false;
    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } = [];

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".graphql", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".gql", StringComparison.OrdinalIgnoreCase);
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath) =>
        ImportType.Internal;

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        throw new NotSupportedException("GraphQLFileParser does not use TreeSitter visitors.");
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

        var content = File.Exists(filePath) ? await File.ReadAllTextAsync(filePath) : string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SyntaxTree(filePath, relativePath, null, null, null, fileNode, this, [], [], []);
        }

        var (operations, sdlFields) = GraphQLDocumentExtractor.ParseDocument(content);

        foreach (var op in operations)
        {
            if (op.RootFields.Count == 0) continue;

            var opName = !string.IsNullOrEmpty(op.OperationName)
                ? op.OperationName!
                : $"Anonymous{op.OperationType}_{op.StartLine}";

            var pascalOp = ToPascalCase(opName);
            var primaryHookName = !string.IsNullOrEmpty(op.OperationName)
                ? $"use{pascalOp}{op.OperationType}"
                : opName;

            var symbolId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:fn:{primaryHookName}";
            var funcNode = new FunctionNode(
                symbolId,
                primaryHookName,
                symbolId,
                relativePath,
                relativePath,
                op.StartLine,
                op.EndLine,
                1,
                1);

            funcNode.SetExtension("graphql_operation", opName);
            funcNode.SetExtension("operation_type", op.OperationType);
            funcNode.SetExtension("graphql_fields", string.Join(",", op.RootFields));
            if (op.RootFields.Count == 1)
            {
                funcNode.SetExtension("graphql_field", op.RootFields[0]);
            }

            if (!string.IsNullOrEmpty(op.OperationName))
            {
                var aliases = BuildCodegenAliases(op.OperationName!, op.OperationType);
                funcNode.SetExtension("graphql_aliases", string.Join(",", aliases));
            }

            foreach (var rootField in op.RootFields)
            {
                var gqlPath = "/" + rootField.TrimStart('/');
                var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:graphql:{op.OperationType}:{rootField}:{symbolId}";
                var extProps = new Dictionary<string, string>
                {
                    ["file_path"] = relativePath,
                    ["start_line"] = op.StartLine.ToString(),
                    ["start_col"] = "1",
                    ["graphql_field"] = rootField,
                    ["operation_type"] = op.OperationType,
                    ["caller_symbol_id"] = symbolId,
                    ["is_internal"] = "true",
                    ["url"] = gqlPath
                };
                var extNode = new ExternalServiceNode(extId, rootField, "graphql", "graphql", gqlPath, extProps);
                funcNode.Children.Add(extNode);
            }

            fileNode.Children.Add(funcNode);
        }

        var resolvedProj = ConstantRegistry.ExtractProjectName(relativePath) ?? "default";
        var projPart = (!string.IsNullOrEmpty(resolvedProj) && resolvedProj != "default") ? $"{resolvedProj}:" : "";

        foreach (var sdl in sdlFields)
        {
            var method = sdl.OperationType.ToUpperInvariant();
            var epName = $"{method}:{sdl.FieldName}";
            var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{projPart}sdl:{method}:{sdl.FieldName}";
            var epNode = new EndpointNode(
                epId,
                epName,
                relativePath,
                method,
                sdl.FieldName,
                "GraphQL",
                false,
                null,
                null,
                null,
                sdl.ReturnType,
                sdl.OperationType);
            epNode.SetExtension("graphql_field", sdl.FieldName);
            epNode.SetExtension("graphql_sdl", "true");
            fileNode.Children.Add(epNode);
        }

        return new SyntaxTree(filePath, relativePath, null, null, null, fileNode, this, [], [], []);
    }

    internal static List<string> BuildCodegenAliases(string opName, string opType)
    {
        var pascal = ToPascalCase(opName);
        var aliases = new List<string>
        {
            opName,
            pascal,
            $"{pascal}Document",
            $"{opName}Document",
            $"use{pascal}{opType}",
            $"{pascal}{opType}"
        };

        if (opType == "Query")
        {
            aliases.Add($"use{pascal}LazyQuery");
            aliases.Add($"use{pascal}SuspenseQuery");
        }

        return [.. aliases.Distinct(StringComparer.Ordinal)];
    }

    internal static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (char.IsUpper(name[0]) && !name.Contains('_') && !name.Contains('-'))
        {
            return name;
        }

        var parts = name.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return char.ToUpperInvariant(name[0]) + name[1..];
        }

        var sb = new StringBuilder(name.Length);
        foreach (var part in parts)
        {
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part[1..]);
        }
        return sb.ToString();
    }
}

/// <summary>
/// Lexical &amp; recursive-descent GraphQL selection-set and SDL parser.
/// Extracts root fields from executable operations (supporting aliases, arguments, directives,
/// inline fragments, and multi-root-field queries) and SDL root types, as well as TypedDocumentNode JSON ASTs.
/// </summary>
public static class GraphQLDocumentExtractor
{
    private readonly record struct GqlToken(string Text, bool IsPunctuator, int Line);

    public static (List<GraphQLParsedOperation> Operations, List<GraphQLParsedSdlField> SdlFields) ParseDocument(string content)
    {
        var operations = new List<GraphQLParsedOperation>();
        var sdlFields = new List<GraphQLParsedSdlField>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return (operations, sdlFields);
        }

        var tokens = Tokenize(content);
        var i = 0;

        while (i < tokens.Count)
        {
            var tok = tokens[i];

            // 1. Executable operation: query / mutation / subscription
            if (!tok.IsPunctuator && tok.Text is "query" or "mutation" or "subscription")
            {
                var startLine = tok.Line;
                var opType = tok.Text switch
                {
                    "mutation" => "Mutation",
                    "subscription" => "Subscription",
                    _ => "Query"
                };
                i++;

                string? opName = null;
                if (i < tokens.Count && !tokens[i].IsPunctuator)
                {
                    opName = tokens[i].Text;
                    i++;
                }

                // Skip variable definitions (...)
                if (i < tokens.Count && tokens[i].Text == "(")
                {
                    i = SkipBalanced(tokens, i, "(", ")");
                }

                // Skip directives @dir(...)
                i = SkipDirectives(tokens, i);

                if (i < tokens.Count && tokens[i].Text == "{")
                {
                    var rootFields = new List<string>();
                    i = ParseRootSelectionSet(tokens, i, rootFields, out var endLine);
                    if (rootFields.Count > 0)
                    {
                        operations.Add(new GraphQLParsedOperation(opType, opName, rootFields, startLine, endLine));
                    }
                    continue;
                }
            }
            // 2. Shorthand anonymous query `{ fieldA { id } }`
            else if (tok.Text == "{")
            {
                var startLine = tok.Line;
                var rootFields = new List<string>();
                i = ParseRootSelectionSet(tokens, i, rootFields, out var endLine);
                if (rootFields.Count > 0)
                {
                    operations.Add(new GraphQLParsedOperation("Query", null, rootFields, startLine, endLine));
                }
                continue;
            }
            // 3. SDL root type definition: `type Query { ... }` or `extend type Query { ... }`
            else if (!tok.IsPunctuator && (tok.Text == "type" || (tok.Text == "extend" && i + 1 < tokens.Count && tokens[i + 1].Text == "type")))
            {
                if (tok.Text == "extend") i++;
                i++; // consume 'type'

                if (i < tokens.Count && !tokens[i].IsPunctuator)
                {
                    var typeName = tokens[i].Text;
                    i++;

                    // Advance until '{'
                    while (i < tokens.Count && tokens[i].Text != "{")
                    {
                        if (tokens[i].Text == "(")
                        {
                            i = SkipBalanced(tokens, i, "(", ")");
                        }
                        else
                        {
                            i++;
                        }
                    }

                    if (i < tokens.Count && tokens[i].Text == "{")
                    {
                        if (typeName is "Query" or "Mutation" or "Subscription")
                        {
                            i = ParseSdlTypeFields(tokens, i, typeName, sdlFields);
                        }
                        else
                        {
                            i = SkipBalanced(tokens, i, "{", "}");
                        }
                        continue;
                    }
                }
            }
            // 4. Other top-level blocks (fragment, input, enum, interface, union, scalar, schema): skip their '{ ... }' block
            else if (tok.Text == "{")
            {
                i = SkipBalanced(tokens, i, "{", "}");
                continue;
            }

            i++;
        }

        return (operations, sdlFields);
    }

    private static int ParseRootSelectionSet(
        List<GqlToken> tokens,
        int openBraceIdx,
        List<string> rootFields,
        out int endLine)
    {
        endLine = tokens[openBraceIdx].Line;
        var i = openBraceIdx + 1;

        while (i < tokens.Count)
        {
            var tok = tokens[i];
            endLine = tok.Line;

            if (tok.Text == "}")
            {
                return i + 1;
            }

            // Fragment spread `...Frag` or inline fragment `... on Query { ... }`
            if (tok.Text == "...")
            {
                i++;
                if (i < tokens.Count && tokens[i].Text == "on")
                {
                    i++; // consume 'on'
                    if (i < tokens.Count && !tokens[i].IsPunctuator)
                    {
                        i++; // consume type name
                    }
                    i = SkipDirectives(tokens, i);
                    if (i < tokens.Count && tokens[i].Text == "{")
                    {
                        i = ParseRootSelectionSet(tokens, i, rootFields, out endLine);
                    }
                }
                else if (i < tokens.Count && tokens[i].Text == "{")
                {
                    i = ParseRootSelectionSet(tokens, i, rootFields, out endLine);
                }
                else
                {
                    if (i < tokens.Count && !tokens[i].IsPunctuator) i++; // fragment name
                    i = SkipDirectives(tokens, i);
                }
                continue;
            }

            if (!tok.IsPunctuator)
            {
                var fieldOrAlias = tok.Text;
                i++;

                // Check for alias: `aliasName: actualFieldName`
                if (i < tokens.Count && tokens[i].Text == ":")
                {
                    i++; // consume ':'
                    if (i < tokens.Count && !tokens[i].IsPunctuator)
                    {
                        fieldOrAlias = tokens[i].Text;
                        i++;
                    }
                }

                if (!fieldOrAlias.StartsWith("__", StringComparison.Ordinal) &&
                    !rootFields.Contains(fieldOrAlias, StringComparer.Ordinal))
                {
                    rootFields.Add(fieldOrAlias);
                }

                // Skip field arguments (...)
                if (i < tokens.Count && tokens[i].Text == "(")
                {
                    i = SkipBalanced(tokens, i, "(", ")");
                }

                // Skip field directives @dir(...)
                i = SkipDirectives(tokens, i);

                // Skip nested selection set { ... }
                if (i < tokens.Count && tokens[i].Text == "{")
                {
                    endLine = tokens[i].Line;
                    i = SkipBalanced(tokens, i, "{", "}");
                    if (i > 0 && i <= tokens.Count)
                    {
                        endLine = tokens[i - 1].Line;
                    }
                }
                continue;
            }

            i++;
        }

        return i;
    }

    private static int ParseSdlTypeFields(
        List<GqlToken> tokens,
        int openBraceIdx,
        string operationType,
        List<GraphQLParsedSdlField> sdlFields)
    {
        var i = openBraceIdx + 1;

        while (i < tokens.Count)
        {
            var tok = tokens[i];
            if (tok.Text == "}")
            {
                return i + 1;
            }

            if (!tok.IsPunctuator)
            {
                var fieldName = tok.Text;
                var line = tok.Line;
                i++;

                // Skip field arguments (...)
                if (i < tokens.Count && tokens[i].Text == "(")
                {
                    i = SkipBalanced(tokens, i, "(", ")");
                }

                string? returnType = null;
                if (i < tokens.Count && tokens[i].Text == ":")
                {
                    i++; // consume ':'
                    var typeSb = new StringBuilder();
                    while (i < tokens.Count && (tokens[i].Text is "[" or "]" or "!" || (!tokens[i].IsPunctuator && typeSb.Length == 0) || (typeSb.Length > 0 && tokens[i - 1].Text == "[" && !tokens[i].IsPunctuator)))
                    {
                        if (!tokens[i].IsPunctuator)
                        {
                            returnType ??= tokens[i].Text;
                        }
                        typeSb.Append(tokens[i].Text);
                        i++;
                    }
                }

                i = SkipDirectives(tokens, i);

                if (!fieldName.StartsWith("__", StringComparison.Ordinal))
                {
                    sdlFields.Add(new GraphQLParsedSdlField(operationType, fieldName, returnType, line));
                }
                continue;
            }

            i++;
        }

        return i;
    }

    private static int SkipDirectives(List<GqlToken> tokens, int i)
    {
        while (i < tokens.Count && tokens[i].Text == "@")
        {
            i++; // '@'
            if (i < tokens.Count && !tokens[i].IsPunctuator)
            {
                i++; // directive name
            }
            if (i < tokens.Count && tokens[i].Text == "(")
            {
                i = SkipBalanced(tokens, i, "(", ")");
            }
        }
        return i;
    }

    private static int SkipBalanced(List<GqlToken> tokens, int startIdx, string open, string close)
    {
        var depth = 0;
        var i = startIdx;
        while (i < tokens.Count)
        {
            if (tokens[i].Text == open) depth++;
            else if (tokens[i].Text == close)
            {
                depth--;
                if (depth <= 0) return i + 1;
            }
            i++;
        }
        return i;
    }

    private static List<GqlToken> Tokenize(string content)
    {
        var tokens = new List<GqlToken>();
        var line = 1;
        var i = 0;
        var len = content.Length;

        while (i < len)
        {
            var c = content[i];

            if (c == '\n')
            {
                line++;
                i++;
                continue;
            }

            if (char.IsWhiteSpace(c) || c == ',')
            {
                i++;
                continue;
            }

            // Line comment # ...
            if (c == '#')
            {
                while (i < len && content[i] != '\n') i++;
                continue;
            }

            // Block string """ ... """
            if (c == '"' && i + 2 < len && content[i + 1] == '"' && content[i + 2] == '"')
            {
                i += 3;
                while (i + 2 < len && !(content[i] == '"' && content[i + 1] == '"' && content[i + 2] == '"'))
                {
                    if (content[i] == '\n') line++;
                    i++;
                }
                i = Math.Min(len, i + 3);
                continue;
            }

            // String literal "..."
            if (c == '"')
            {
                i++;
                while (i < len && content[i] != '"')
                {
                    if (content[i] == '\\' && i + 1 < len) i++;
                    else if (content[i] == '\n') line++;
                    i++;
                }
                if (i < len) i++;
                continue;
            }

            // Spread operator ...
            if (c == '.' && i + 2 < len && content[i + 1] == '.' && content[i + 2] == '.')
            {
                tokens.Add(new GqlToken("...", true, line));
                i += 3;
                continue;
            }

            // Single-char punctuators
            if (c is '{' or '}' or '(' or ')' or '[' or ']' or ':' or '=' or '@' or '$' or '!' or '|' or '&')
            {
                tokens.Add(new GqlToken(c.ToString(), true, line));
                i++;
                continue;
            }

            // Name / Identifier / Number
            if (char.IsLetterOrDigit(c) || c is '_' or '-')
            {
                var start = i;
                while (i < len && (char.IsLetterOrDigit(content[i]) || content[i] is '_' or '-'))
                {
                    i++;
                }
                tokens.Add(new GqlToken(content[start..i], false, line));
                continue;
            }

            i++;
        }

        return tokens;
    }

    private static readonly Regex TypedDocOperationRegex = new(
        @"[""']?kind[""']?\s*:\s*[""']OperationDefinition[""'][\s\S]*?[""']?operation[""']?\s*:\s*[""'](query|mutation|subscription)[""'](?:[\s\S]*?[""']?name[""']?\s*:\s*\{\s*(?:[""']?kind[""']?\s*:\s*[""']Name[""']\s*,\s*)?[""']?value[""']?\s*:\s*[""']([A-Za-z0-9_]+)[""'])?",
        RegexOptions.Compiled);

    private static readonly Regex TypedDocFieldRegex = new(
        @"[""']?kind[""']?\s*:\s*[""']Field[""'][\s\S]*?[""']?name[""']?\s*:\s*\{\s*(?:[""']?kind[""']?\s*:\s*[""']Name[""']\s*,\s*)?[""']?value[""']?\s*:\s*[""']([A-Za-z0-9_]+)[""']",
        RegexOptions.Compiled);

    /// <summary>
    /// Extracts operation metadata from a compiled TypedDocumentNode / DocumentNode JSON AST literal
    /// (e.g. `{ kind: "Document", definitions: [{ kind: "OperationDefinition", operation: "query", ... }] }`).
    /// </summary>
    public static GraphQLParsedOperation? TryParseTypedDocumentAst(string objectLiteralText, int startLine = 1, int endLine = 1)
    {
        if (string.IsNullOrWhiteSpace(objectLiteralText) ||
            !objectLiteralText.Contains("OperationDefinition", StringComparison.Ordinal))
        {
            return null;
        }

        var opMatch = TypedDocOperationRegex.Match(objectLiteralText);
        if (!opMatch.Success)
        {
            return null;
        }

        var rawOp = opMatch.Groups[1].Value.ToLowerInvariant();
        var opType = rawOp switch
        {
            "mutation" => "Mutation",
            "subscription" => "Subscription",
            _ => "Query"
        };
        var opName = opMatch.Groups[2].Success ? opMatch.Groups[2].Value : null;

        // Extract top-level Field selections from the first SelectionSet
        var rootFields = new List<string>();
        var selSetIdx = objectLiteralText.IndexOf("selectionSet", opMatch.Index, StringComparison.Ordinal);
        if (selSetIdx >= 0)
        {
            var fieldMatch = TypedDocFieldRegex.Match(objectLiteralText, selSetIdx);
            if (fieldMatch.Success)
            {
                var fieldName = fieldMatch.Groups[1].Value;
                if (!string.IsNullOrEmpty(fieldName) && !fieldName.StartsWith("__", StringComparison.Ordinal))
                {
                    rootFields.Add(fieldName);
                }
            }
        }

        if (rootFields.Count == 0) return null;
        return new GraphQLParsedOperation(opType, opName, rootFields, startLine, endLine);
    }
}
