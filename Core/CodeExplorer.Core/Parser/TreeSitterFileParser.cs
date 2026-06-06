using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public static class TreeSitterFileParser
{
    public static async Task<SyntaxTree> ParseFileAsync(
        string filePath,
        string relativePath,
        string parentNodeId,
        IFileParser fileParser,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        filePath = filePath.Replace('\\', '/');
        relativePath = relativePath.Replace('\\', '/');
        var sourceText = await File.ReadAllTextAsync(filePath);
        var language = new Language(fileParser.LanguageName);
        var parser = new TreeSitter.Parser(language);
        var tree = parser.Parse(sourceText);

        var fileNodeId = $"{workspaceId}:file:{relativePath}";
        var fileNode = new FileNode(fileNodeId, Path.GetFileName(filePath), relativePath, filePath);

        var rawImports = new List<RawImport>();
        var rawVariables = new List<RawVariable>();
        var rawTypeBindings = new List<RawTypeBinding>();

        if (tree != null)
        {
            // First pass: collect imports to identify active library parsers
            var preVisitor = fileParser.CreateVisitor(tree.RootNode, new List<ILibraryParser>());
            preVisitor.Visit(tree.RootNode);
            rawImports = preVisitor.RawImports.Select(ri => ri with {
                FilePath = relativePath,
                Type = fileParser.ResolveImportType(ri.Path, relativePath, absoluteWorkspacePath)
            }).ToList();

            // Match imports to find active library parsers
            var fileImports = rawImports
                .Where(i => i.Type == ImportType.External)
                .Select(i => i.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var registry = new LibraryTrieRegistry(fileParser.LibraryParsers);
            var detectedParsers = new List<ILibraryParser>();
            foreach (var import in fileImports)
            {
                var match = registry.Match(import);
                if (match != null && !detectedParsers.Contains(match))
                {
                    detectedParsers.Add(match);
                }
            }

            foreach (var lp in detectedParsers.Where(p => !p.IsImplemented))
            {
                Console.WriteLine($"Library '{lp.Name}' detected but parser is not implemented yet.");
            }

            var activeLibraryParsers = detectedParsers
                .Where(lp => lp.IsImplemented)
                .ToList();

            // Second pass: build the actual ontology tree and collect all semantic data
            var mainVisitor = fileParser.CreateVisitor(tree.RootNode, activeLibraryParsers);
            mainVisitor.Visit(tree.RootNode);

            // Map syntactic tree to ontology nodes
            foreach (var childSyntactic in mainVisitor.RootSymbol.Children)
            {
                var childNode = MapSyntacticSymbolToOntology(childSyntactic, Path.GetFileName(filePath), relativePath, workspaceId, fileNode.Id);
                fileNode.Children.Add(childNode);
            }

            foreach (var reference in mainVisitor.RootSymbol.References)
            {
                fileNode.References.Add(reference with { ScopeSymbolId = fileNode.Id });
            }

            rawImports = mainVisitor.RawImports.Select(ri => ri with {
                FilePath = relativePath,
                Type = fileParser.ResolveImportType(ri.Path, relativePath, absoluteWorkspacePath)
            }).ToList();

            rawVariables = mainVisitor.RawVariables.Select(rv => rv with { FilePath = relativePath }).ToList();
            rawTypeBindings = mainVisitor.RawTypeBindings.Select(rt => rt with { FilePath = relativePath }).ToList();
        }
        else
        {
            parser.Dispose();
            language.Dispose();
        }

        Console.WriteLine($"Finished parsing file: {relativePath} with {fileNode.Children.Count} top-level symbols.");
        return new SyntaxTree(filePath, relativePath, tree, parser, language, fileNode, rawImports, rawVariables, rawTypeBindings);
    }

    private static IOntologyNode MapSyntacticSymbolToOntology(
        SyntacticSymbol syntactic,
        string fileName,
        string relativePath,
        string workspaceId,
        string parentScopeId)
    {
        var node = syntactic.Node;
        var kind = syntactic.Kind;
        var name = syntactic.Name;

        var symbolId = $"{workspaceId}:symbol:{relativePath}:{kind}:{name}:{node.StartPosition.Row}";

        IOntologyNode typedNode = kind switch
        {
            OntologyConstants.NodeLabels.Class => new ClassNode(symbolId, name, symbolId, fileName, relativePath, node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
            OntologyConstants.NodeLabels.Interface => new InterfaceNode(symbolId, name, symbolId, fileName, relativePath, node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
            OntologyConstants.NodeLabels.Function => new FunctionNode(symbolId, name, symbolId, fileName, relativePath, node.StartPosition.Row, node.EndPosition.Row, node.StartPosition.Column, node.EndPosition.Column),
            OntologyConstants.NodeLabels.Query => NestedSqlParser.ParseNestedSql(syntactic.Text ?? node.Text, symbolId, relativePath) ?? new QueryNode(symbolId, name, NestedSqlParser.CleanQueryText(syntactic.Text ?? node.Text), relativePath),
            OntologyConstants.NodeLabels.EntryPoint => CreateEntryPointNode(name, node, relativePath, workspaceId),
            OntologyConstants.NodeLabels.ExternalService => CreateExternalServiceNode(name, node, relativePath, workspaceId),
            _ => throw new InvalidOperationException($"Unsupported symbol type: {kind}")
        };

        // Recursively map children
        foreach (var childSyntactic in syntactic.Children)
        {
            var childNode = MapSyntacticSymbolToOntology(childSyntactic, fileName, relativePath, workspaceId, symbolId);
            typedNode.Children.Add(childNode);
        }

        // Rewrite references to use the correct parent scope ID if empty
        foreach (var reference in syntactic.References)
        {
            var resolvedScopeId = string.IsNullOrEmpty(reference.ScopeSymbolId) ? symbolId : reference.ScopeSymbolId;
            typedNode.References.Add(reference with { ScopeSymbolId = resolvedScopeId });
        }

        return typedNode;
    }

    private static EntryPointNode CreateEntryPointNode(string name, Node node, string relativePath, string workspaceId)
    {
        var projectName = GetProjectNameFromRelativePath(relativePath);
        if (string.IsNullOrEmpty(projectName)) projectName = "default";

        var protocol = "http";
        var route = name;

        if (name.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "ws";
            route = name.Substring(3);
        }
        else if (name.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "event";
            route = name.Substring(6);
        }
        else if (name.Contains(':'))
        {
            var idx = name.IndexOf(':');
            route = name.Substring(idx + 1);
        }

        var entryPointId = $"{workspaceId}:entrypoint:{projectName}:{protocol}:{name.Replace(":", "_")}";
        var ext = new Dictionary<string, string>
        {
            { "file_path", relativePath },
            { "start_line", node.StartPosition.Row.ToString() }
        };
        return new EntryPointNode(entryPointId, name.Replace(":", " "), protocol, route, relativePath, ext);
    }

    private static ExternalServiceNode CreateExternalServiceNode(string name, Node node, string relativePath, string workspaceId)
    {
        var protocol = "http";
        var domainOrService = name;

        if (name.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "ws";
            domainOrService = name.Substring(3);
        }
        else if (name.Contains(':'))
        {
            var idx = name.IndexOf(':');
            protocol = name.Substring(0, idx);
            domainOrService = name.Substring(idx + 1);
        }

        var extServiceId = $"{workspaceId}:externalservice:{protocol}:{domainOrService}";
        var ext = new Dictionary<string, string>
        {
            { "file_path", relativePath },
            { "start_line", node.StartPosition.Row.ToString() }
        };
        return new ExternalServiceNode(extServiceId, domainOrService, protocol, domainOrService, relativePath, ext);
    }

    private static string GetProjectNameFromRelativePath(string relativePath)
    {
        var cleanPath = relativePath.Replace('\\', '/').Trim('/');
        var parts = cleanPath.Split('/');
        if (parts.Length == 0) return "default";

        if (parts.Length >= 2 && (parts[0] is "Core" or "Parsers" or "Tests"))
        {
            return parts[1];
        }
        return parts[0];
    }
}
