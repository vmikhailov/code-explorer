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
        var recordInheritance = new Dictionary<string, string>();

        foreach (var filePath in filePaths)
        {
            var source = await File.ReadAllTextAsync(filePath);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            foreach (var record in root.DescendantNodes().OfType<RecordDeclarationSyntax>())
            {
                var info = TryExtractNode(record);
                if (info is not null)
                {
                    results.Add(info);
                    var baseType = record.BaseList?.Types.FirstOrDefault()?.Type.ToString();
                    if (!string.IsNullOrEmpty(baseType))
                    {
                        recordInheritance[info.ClassName] = baseType;
                    }
                }
            }
        }

        // Inherit properties from base record if current node has none
        var nodeByClassName = results.ToDictionary(n => n.ClassName, n => n);
        foreach (var (className, baseTypeName) in recordInheritance)
        {
            if (nodeByClassName.TryGetValue(className, out var derivedNode) &&
                derivedNode.Properties.Count == 0 &&
                nodeByClassName.TryGetValue(baseTypeName, out var baseNode))
            {
                derivedNode.Properties.AddRange(baseNode.Properties);
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

        // New signature: OntologyNode(label, idScheme, purpose, layer, icon, order, pluralLabel, isSystemNode)
        var argList = nodeAttr.ArgumentList.Arguments;
        string label, idScheme, purpose, layer;
        var icon = DefaultNodeIcon;
        var order = 0;
        var pluralLabel = string.Empty;
        var isSystemNode = false;

        if (argList.Count >= 3)
        {
            label = ResolveConstant(argList.Named(ArgLabel) ?? argList[0].Expression);
            idScheme = Unquote(argList.Named(ArgIdScheme) ?? argList[1].Expression);
            purpose = Unquote(argList.Named(ArgPurpose) ?? argList[2].Expression);

            var layerExpr = argList.Named(ArgLayer) ?? PositionalArgAt(argList, 3);
            layer = layerExpr is not null ? ResolveConstant(layerExpr) : DefaultSemanticLayer;

            var iconExpr = argList.Named(ArgIcon) ?? PositionalArgAt(argList, 4);
            if (iconExpr is not null)
            {
                icon = Unquote(iconExpr);
            }

            var orderExpr = argList.Named(ArgOrder) ?? PositionalArgAt(argList, 5);
            if (orderExpr is not null && int.TryParse(orderExpr.ToString().Trim(), out var parsedOrder))
            {
                order = parsedOrder;
            }

            var pluralExpr = argList.Named(ArgPluralLabel) ?? PositionalArgAt(argList, 6);
            pluralLabel = pluralExpr is not null ? Unquote(pluralExpr) : label;

            var sysExpr = argList.Named(ArgIsSystemNode) ?? PositionalArgAt(argList, 7);
            if (sysExpr is not null)
            {
                isSystemNode = string.Equals(sysExpr.ToString().Trim(), BoolTrueLiteral, StringComparison.OrdinalIgnoreCase);
            }
        }
        else
        {
            // Fallback / legacy form: first arg is purpose string
            label = record.Identifier.Text.Replace(NodeClassSuffix, string.Empty);
            idScheme = string.Empty;
            purpose = Unquote(argList[0].Expression);
            layer = DefaultSemanticLayer;
            pluralLabel = label;
        }

        // Outbound edges: [OntologyEdge<TTo>(rel)] — generic attribute
        var outEdges = ExtractNodeOutEdges(record, label);

        // Properties: constructor params or member properties with [OntologyProperty("desc")]
        var props = ExtractConstructorProperties(record);
        props.AddRange(ExtractBodyProperties(record));

        return new NodeInfo(record.Identifier.Text, label, idScheme, purpose, layer, outEdges, props, icon, order, pluralLabel, isSystemNode);
    }

    private static ExpressionSyntax? PositionalArgAt(SeparatedSyntaxList<AttributeArgumentSyntax> args, int index) =>
        args.Count > index && args[index].NameColon is null && args[index].NameEquals is null
            ? args[index].Expression
            : null;

    private static List<EdgeInfo> ExtractNodeOutEdges(RecordDeclarationSyntax record, string label) =>
        record.AttributeLists
            .SelectMany(al => al.Attributes)
            .Where(a => a.Name is GenericNameSyntax g && g.Identifier.Text == AttrOntologyEdge)
            .Select(a =>
            {
                var generic = (GenericNameSyntax)a.Name;
                var toType = generic.TypeArgumentList.Arguments[0].ToString();
                var relExpr = a.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
                var rel = relExpr != null ? ResolveConstant(relExpr) : string.Empty;
                return new EdgeInfo(label, rel, toType);
            })
            .ToList();

    private static List<PropertyInfo> ExtractConstructorProperties(RecordDeclarationSyntax record) =>
        (record.ParameterList?.Parameters ?? [])
            .Select(p =>
            {
                var attrs = p.AttributeLists.SelectMany(al => al.Attributes).ToList();
                var propAttr = attrs.FirstOrDefault(a => GetSimpleName(a.Name) == AttrOntologyProperty);
                if (propAttr is null || IsSystemOntologyProperty(propAttr)) return null;

                var desc = propAttr.ArgumentList?.Arguments.FirstOrDefault()?.Expression is { } expr ? Unquote(expr) : string.Empty;
                var cypherName = ResolveJsonOrSnakeName(attrs, p.Identifier.Text);
                return new PropertyInfo(p.Identifier.Text, p.Type?.ToString() ?? UnknownTypeFallback, desc, cypherName);
            })
            .OfType<PropertyInfo>()
            .ToList();

    private static IEnumerable<PropertyInfo> ExtractBodyProperties(RecordDeclarationSyntax record) =>
        record.Members
            .OfType<PropertyDeclarationSyntax>()
            .Select(p =>
            {
                var attrs = p.AttributeLists.SelectMany(al => al.Attributes).ToList();
                var propAttr = attrs.FirstOrDefault(a => GetSimpleName(a.Name) == AttrOntologyProperty);
                if (propAttr is null || IsSystemOntologyProperty(propAttr)) return null;

                var desc = propAttr.ArgumentList?.Arguments.FirstOrDefault()?.Expression is { } expr ? Unquote(expr) : string.Empty;
                var cypherName = ResolveJsonOrSnakeName(attrs, p.Identifier.Text);
                return new PropertyInfo(p.Identifier.Text, p.Type.ToString(), desc, cypherName);
            })
            .OfType<PropertyInfo>();

    private static string ResolveJsonOrSnakeName(List<AttributeSyntax> attrs, string pascalName)
    {
        var jsonAttr = attrs.FirstOrDefault(a => GetSimpleName(a.Name) is AttrJsonPropertyName or AttrJsonPropertyNameFull);
        var jsonNameExpr = jsonAttr?.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
        if (jsonNameExpr is not null)
        {
            var resolved = Unquote(jsonNameExpr);
            if (!string.IsNullOrEmpty(resolved))
            {
                return resolved;
            }
        }
        return ToSnakeCase(pascalName);
    }

    internal static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new System.Text.StringBuilder(input.Length + 4);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (i > 0 && char.IsUpper(c))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
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

    private const string DefaultNodeIcon = "symbol-class";
    private const string DefaultSemanticLayer = "Layer 4: Semantic Structure";
    private const string NodeClassSuffix = "Node";
    private const string UnknownTypeFallback = "?";
    private const string AttrOntologyEdge = "OntologyEdge";
    private const string AttrOntologyProperty = "OntologyProperty";
    private const string AttrJsonPropertyName = "JsonPropertyName";
    private const string AttrJsonPropertyNameFull = "JsonPropertyNameAttribute";
    private const string ArgLabel = "label";
    private const string ArgIdScheme = "idScheme";
    private const string ArgPurpose = "purpose";
    private const string ArgLayer = "layer";
    private const string ArgIcon = "icon";
    private const string ArgOrder = "order";
    private const string ArgPluralLabel = "pluralLabel";
    private const string ArgIsSystemNode = "isSystemNode";
    private const string ArgIsSystem = "isSystem";
    private const string ArgIsSystemPascal = "IsSystem";
    private const string BoolTrueLiteral = "true";

    private static bool IsSystemOntologyProperty(AttributeSyntax propAttr)
    {
        if (propAttr.ArgumentList is null) return false;
        var args = propAttr.ArgumentList.Arguments;
        var namedExpr = args.Named(ArgIsSystem) ?? args.Named(ArgIsSystemPascal);
        if (namedExpr is not null)
        {
            return string.Equals(namedExpr.ToString().Trim(), BoolTrueLiteral, StringComparison.OrdinalIgnoreCase);
        }
        return args.Count >= 2 &&
               args[1].NameColon is null &&
               args[1].NameEquals is null &&
               string.Equals(args[1].Expression.ToString().Trim(), BoolTrueLiteral, StringComparison.OrdinalIgnoreCase);
    }

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
