using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OntologyGen;

/// <summary>
/// Extracts ontology metadata from .cs source files using Roslyn syntax-only parsing.
/// No compilation or assembly loading — reads attribute arguments directly from the AST.
/// </summary>
public sealed class OntologyExtractor
{
    public async Task<List<NodeInfo>> ExtractNodesAsync(IEnumerable<string> filePaths)
    {
        InitializeConstantsForFiles(filePaths);
        var results = new List<NodeInfo>();

        foreach (var filePath in filePaths)
        {
            var source = await File.ReadAllTextAsync(filePath);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            foreach (var record in root.DescendantNodes().OfType<RecordDeclarationSyntax>())
            {
                var info = TryExtractNode(record);
                if (info is not null)
                    results.Add(info);
            }
        }

        return results;
    }

    public async Task<List<RelInfo>> ExtractRelationshipsAsync(IEnumerable<string> filePaths)
    {
        var results = new List<RelInfo>();

        foreach (var filePath in filePaths)
        {
            var source = await File.ReadAllTextAsync(filePath);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            foreach (var record in root.DescendantNodes().OfType<RecordDeclarationSyntax>())
            {
                var info = TryExtractRelationship(record);
                if (info is not null)
                    results.Add(info);
            }
        }

        return results;
    }

    // ── Node extraction ──────────────────────────────────────────────────────

    private static NodeInfo? TryExtractNode(RecordDeclarationSyntax record)
    {
        // Look for [OntologyNode(...)] — non-generic, plain name
        var nodeAttr = record.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => GetSimpleName(a.Name) == "OntologyNode"
                              && a.Name is not GenericNameSyntax);

        if (nodeAttr?.ArgumentList is null)
            return null;

        // New signature: OntologyNode(label, idScheme, purpose)
        var argList = nodeAttr.ArgumentList.Arguments;
        string label, idScheme, purpose, layer;

        if (argList.Count >= 3)
        {
            label = ResolveConstant(argList.Named("label") ?? argList[0].Expression);
            idScheme = Unquote(argList.Named("idScheme") ?? argList[1].Expression);
            purpose = Unquote(argList.Named("purpose") ?? argList[2].Expression);

            var layerExpr = argList.Named("layer") ?? (argList.Count >= 4 ? argList[3].Expression : null);
            layer = layerExpr is not null ? ResolveConstant(layerExpr) : "Layer 4: Semantic Structure";
        }
        else
        {
            // Fallback / legacy form: first arg is purpose string
            label = record.Identifier.Text.Replace("Node", ""); // best-effort from class name
            idScheme = "";
            purpose = Unquote(argList[0].Expression);
            layer = "Layer 4: Semantic Structure";
        }

        // Outbound edges: [OntologyEdge<TTo>(rel)] — generic attribute
        var outEdges = record.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => a.Name is GenericNameSyntax g && g.Identifier.Text == "OntologyEdge")
            .Select(a =>
            {
                var generic = (GenericNameSyntax)a.Name;
                var toType = generic.TypeArgumentList.Arguments[0].ToString();   // e.g. "ClassNode"
                var relExpr = a.ArgumentList!.Arguments[0].Expression;
                var rel = ResolveConstant(relExpr);
                return new EdgeInfo(label, rel, toType);
            })
            .ToList();

        // Properties: constructor params with [OntologyProperty("desc")]
        var props = (record.ParameterList?.Parameters ?? [])
            .Select(p =>
            {
                var propAttr = p.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .FirstOrDefault(a => GetSimpleName(a.Name) == "OntologyProperty");

                if (propAttr is null) return null;

                var desc = Unquote(propAttr.ArgumentList!.Arguments[0].Expression);
                return new PropertyInfo(p.Identifier.Text, p.Type?.ToString() ?? "?", desc);
            })
            .OfType<PropertyInfo>()
            .ToList();

        return new NodeInfo(record.Identifier.Text, label, idScheme, purpose, layer, outEdges, props);
    }

    // ── Relationship extraction ──────────────────────────────────────────────

    private static RelInfo? TryExtractRelationship(RecordDeclarationSyntax record)
    {
        var attr = record.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => GetSimpleName(a.Name) == "OntologyRelationship");

        if (attr?.ArgumentList is null)
            return null;

        var argList = attr.ArgumentList.Arguments;
        var label = ResolveConstant(argList.Named("label") ?? argList[0].Expression);
        var desc = Unquote(argList.Named("description") ?? argList[1].Expression);

        return new RelInfo(label, desc);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Gets simple identifier name regardless of generic or qualified form.</summary>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        GenericNameSyntax g => g.Identifier.Text,
        SimpleNameSyntax s => s.Identifier.Text,
        QualifiedNameSyntax q => GetSimpleName(q.Right),
        _ => name.ToString()
    };

    /// <summary>Strips outer quotes from a string literal expression.</summary>
    internal static string Unquote(ExpressionSyntax? expr)
    {
        if (expr is null) return "";
        var raw = expr.ToString().Trim();
        // Handle verbatim (@"...") and regular ("...") strings
        if (raw.StartsWith("@\"") && raw.EndsWith("\""))
            return raw[2..^1].Replace("\"\"", "\"");
        if (raw.StartsWith('"') && raw.EndsWith('"'))
            return raw[1..^1];
        // Concatenated strings: "foo" + "bar" — just strip all quotes/operators (best-effort)
        return raw.Replace("\" +", "").Replace("+ \"", "").Replace("\"", "").Replace("@", "").Trim();
    }

    private static readonly Dictionary<string, string> _constantsMap = new();

    private static void InitializeConstantsForFiles(IEnumerable<string> filePaths)
    {
        var firstFile = filePaths.FirstOrDefault();
        if (firstFile == null) return;

        var currentDir = Path.GetDirectoryName(firstFile);
        while (currentDir != null)
        {
            var constantsPath = Path.Combine(currentDir, "OntologyConstants.cs");
            if (File.Exists(constantsPath))
            {
                InitializeConstants(currentDir);
                break;
            }
            currentDir = Path.GetDirectoryName(currentDir);
        }
    }

    private static void InitializeConstants(string commonDir)
    {
        var constantsPath = Path.Combine(commonDir, "OntologyConstants.cs");
        if (!File.Exists(constantsPath)) return;

        try
        {
            var source = File.ReadAllText(constantsPath);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var className = classDecl.Identifier.Text;
                
                var parentClassNames = new List<string> { className };
                var parent = classDecl.Parent;
                while (parent is ClassDeclarationSyntax parentClass)
                {
                    parentClassNames.Insert(0, parentClass.Identifier.Text);
                    parent = parentClass.Parent;
                }
                var fullPrefix = string.Join(".", parentClassNames);

                foreach (var fieldDecl in classDecl.Members.OfType<FieldDeclarationSyntax>())
                {
                    if (fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                    {
                        foreach (var variable in fieldDecl.Declaration.Variables)
                        {
                            var name = variable.Identifier.Text;
                            var valExpr = variable.Initializer?.Value;
                            if (valExpr != null)
                            {
                                var val = Unquote(valExpr);
                                _constantsMap[$"{fullPrefix}.{name}"] = val;
                                
                                if (parentClassNames.Count > 1)
                                {
                                    var relativePrefix = string.Join(".", parentClassNames.Skip(1));
                                    _constantsMap[$"{relativePrefix}.{name}"] = val;
                                }
                                
                                _constantsMap[name] = val;
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Resolves a constant expression to its bare string value.
    /// - Member access (OntologyConstants.NodeLabels.Class) -> "Class"
    /// - String literal ("Zoo") -> "Zoo"
    /// </summary>
    internal static string ResolveConstant(ExpressionSyntax? expr)
    {
        if (expr is null) return "";
        if (expr is LiteralExpressionSyntax) return Unquote(expr);
        var text = expr.ToString().Trim();
        if (_constantsMap.TryGetValue(text, out var val))
            return val;
        
        var dot = text.LastIndexOf('.');
        var segment = dot >= 0 ? text[(dot + 1)..] : text;
        if (_constantsMap.TryGetValue(segment, out val))
            return val;

        return segment;
    }

    private static string StripPrefix(ExpressionSyntax expr, string prefix)
    {
        var text = expr.ToString();
        return text.StartsWith(prefix, StringComparison.Ordinal) ? text[prefix.Length..] : text;
    }

    private static bool HasNamedArg(SeparatedSyntaxList<AttributeArgumentSyntax> args, string name) =>
        args.Any(a => a.NameColon?.Name.Identifier.Text == name
                   || a.NameEquals?.Name.Identifier.Text == name);

    private static bool IsConstantRef(ExpressionSyntax expr) =>
        expr is MemberAccessExpressionSyntax;
}

// ── ArgumentList helpers ─────────────────────────────────────────────────────

internal static class AttributeArgumentListExtensions
{
    /// <summary>Returns the expression for a named argument, or null if not found.</summary>
    public static ExpressionSyntax? Named(
        this SeparatedSyntaxList<AttributeArgumentSyntax> args, string name) =>
        args.FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == name
                               || a.NameColon?.Name.Identifier.Text == name)
            ?.Expression;
}
