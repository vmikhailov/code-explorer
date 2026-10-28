using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust;

/// <summary>
/// Tree-sitter AST visitor for Rust source files (.rs).
/// Handles structs, enums, traits, inherent and trait impls, functions, calls, macros, and embedded SQL.
/// </summary>
public class RustFileVisitor : BaseParserVisitor
{
    private readonly RustParser _parser;
    private readonly CargoManifest? _currentManifest;
    private readonly Dictionary<string, SyntacticSymbol> _typesByName = new(StringComparer.Ordinal);

    public RustFileVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        RustParser parser,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry,
        CargoManifest? currentManifest = null)
        : base(rootNode, activeExtensions, relativePath, absoluteWorkspacePath, fileParser, extensionRegistry)
    {
        _parser = parser;
        _currentManifest = currentManifest;

        if (_currentManifest != null)
        {
            var allDeps = _currentManifest.Dependencies
                .Concat(_currentManifest.DevDependencies)
                .Concat(_currentManifest.BuildDependencies);
            foreach (var dep in allDeps)
            {
                ResolveAndInjectLibraryParser(dep.Name);
            }
        }
    }

    private void AutoActivateFromNode(Node node)
    {
        var text = node.Text;
        if (string.IsNullOrEmpty(text)) return;

        // 1. Text-based heuristic activations
        if (text.Contains("tonic")) ResolveAndInjectLibraryParser("tonic");
        if (text.Contains("basic_publish") || text.Contains("basic_consume") || text.Contains("lapin")) ResolveAndInjectLibraryParser("lapin");
        if (text.Contains("FutureRecord") || text.Contains("StreamConsumer") || text.Contains("rdkafka")) ResolveAndInjectLibraryParser("rdkafka");
        if (text.Contains("async_nats") || text.Contains("async-nats")) ResolveAndInjectLibraryParser("async-nats");
        if (text.Contains("reqwest")) ResolveAndInjectLibraryParser("reqwest");
        if (text.Contains("hyper::") || text.Contains("hyper")) ResolveAndInjectLibraryParser("hyper");
        if (text.Contains("sqlx") || text.Contains("query!") || text.Contains("query_as!")) ResolveAndInjectLibraryParser("sqlx");
        if (text.Contains("sea_orm") || text.Contains("DeriveEntityModel")) ResolveAndInjectLibraryParser("sea-orm");
        if (text.Contains("diesel") || text.Contains("table!")) ResolveAndInjectLibraryParser("diesel");
        if (text.Contains("axum") || text.Contains("Router") || (text.Contains(".route(") && (text.Contains("get(") || text.Contains("post(")))) ResolveAndInjectLibraryParser("axum");
        if (text.Contains("actix_web") || text.Contains("actix-web")) ResolveAndInjectLibraryParser("actix-web");
        if (text.Contains("rocket")) ResolveAndInjectLibraryParser("rocket");
        if (text.Contains("aws_sdk_") || text.Contains("aws-sdk-") || text.Contains("aws_sdk") || text.Contains("receive_message") || text.Contains(".publish()")) ResolveAndInjectLibraryParser("aws-sdk-rust");

        // 2. Attribute-based routing
        if (node.Type == TreeSitterSyntax.Rust.AttributeItem)
        {
            if (text.Contains("get(") || text.Contains("post(") || text.Contains("put(") || text.Contains("delete("))
            {
                ResolveAndInjectLibraryParser("actix-web");
                ResolveAndInjectLibraryParser("rocket");
            }
        }

        // 3. Scoped use/path imports: e.g. use tonic::..., #[tonic::...], client::...
        if (text.Contains("::", StringComparison.Ordinal))
        {
            var cleaned = text.TrimStart('#', '[', ']', '(', '{', ' ', '\t');
            if (cleaned.StartWithIC("use "))
            {
                cleaned = cleaned["use ".Length..].TrimStart();
            }
            if (cleaned.StartWithIC("pub use "))
            {
                cleaned = cleaned["pub use ".Length..].TrimStart();
            }
            var prefix = cleaned.Split("::")[0].Trim();
            if (!string.IsNullOrEmpty(prefix) && prefix != "crate" && prefix != "self" && prefix != "super")
            {
                ResolveAndInjectLibraryParser(prefix);
                if (prefix.Contains('_'))
                {
                    ResolveAndInjectLibraryParser(prefix.Replace('_', '-'));
                }
            }
        }
    }

    protected override string? MapNodeType(Node node)
    {
        AutoActivateFromNode(node);
        return MapNodeTypeUsingLibraries(node) ?? base.MapNodeType(node);
    }

    protected override string? ExtractIdentifier(Node node)
    {
        AutoActivateFromNode(node);
        var kind = MapNodeType(node);
        if (kind != null)
        {
            var libIdent = ExtractIdentifierUsingLibraries(node, kind);
            if (libIdent != null) return libIdent;
        }
        return base.ExtractIdentifier(node);
    }

    protected override void CollectCustomReferencesForSymbol(
        Node node,
        SyntacticSymbol symbolNode,
        SyntacticSymbol parentNode)
    {
        if (symbolNode.Kind == OntologyConstants.NodeLabels.Function)
        {
            var isAsync = node.Children.Any(c => c.Text == "async");
            var isUnsafe = node.Children.Any(c => c.Text == "unsafe");

            var visNode = node.FindChildOfType(TreeSitterSyntax.Rust.VisibilityModifier);
            var visibility = visNode.IsValid() ? visNode.Text : "private";

            symbolNode.Properties["is_async"] = isAsync ? "true" : "false";
            symbolNode.Properties["is_unsafe"] = isUnsafe ? "true" : "false";
            symbolNode.Properties["visibility"] = visibility;
        }
        else if (symbolNode.Kind is "Struct" or "Enum" or "Interface")
        {
            _typesByName[symbolNode.Name] = symbolNode;
        }
    }

    protected override void VisitClassDeclaration(Node node, int depth)
    {
        base.VisitClassDeclaration(node, depth);
        var name = ExtractIdentifier(node);
        if (!string.IsNullOrEmpty(name))
        {
            var sym = RootSymbol.Children.LastOrDefault(c => c.Name == name);
            if (sym != null)
            {
                _typesByName[name] = sym;
            }
        }
    }

    protected override void VisitInterfaceDeclaration(Node node, int depth)
    {
        base.VisitInterfaceDeclaration(node, depth);
        var name = ExtractIdentifier(node);
        if (!string.IsNullOrEmpty(name))
        {
            var sym = RootSymbol.Children.LastOrDefault(c => c.Name == name);
            if (sym != null)
            {
                _typesByName[name] = sym;
            }
        }
    }

    protected override void VisitDefault(Node node, int depth)
    {
        if (node.Type == TreeSitterSyntax.Rust.ImplItem)
        {
            VisitImplItem(node, depth);
            return;
        }

        base.VisitDefault(node, depth);
    }

    private void VisitImplItem(Node node, int depth)
    {
        var (targetTypeName, traitTypeName) = ExtractImplTypes(node);
        if (string.IsNullOrEmpty(targetTypeName))
        {
            VisitChildren(node, depth);
            return;
        }

        if (!_typesByName.TryGetValue(targetTypeName, out var targetSymbol))
        {
            targetSymbol = RootSymbol.Children.FirstOrDefault(c => c.Kind is "Struct" or "Enum" or "Class" && c.Name == targetTypeName);
            if (targetSymbol == null)
            {
                targetSymbol = new SyntacticSymbol("Struct", targetTypeName, node);
                targetSymbol.Properties["type_kind"] = "struct";
                RootSymbol.Children.Add(targetSymbol);
            }
            _typesByName[targetTypeName] = targetSymbol;
        }

        if (!string.IsNullOrEmpty(traitTypeName))
        {
            if (!targetSymbol.References.Any(r => r.Kind == OntologyConstants.Relationships.Implements && r.TargetName == traitTypeName))
            {
                targetSymbol.References.Add(new Reference("", traitTypeName, OntologyConstants.Relationships.Implements));
            }
        }

        SymbolStack.Push(targetSymbol);
        PushedNodeIds.Push(node.Id);

        try
        {
            VisitChildren(node, depth);
        }
        finally
        {
            if (PushedNodeIds.Count > 0 && PushedNodeIds.Peek() == node.Id)
            {
                PushedNodeIds.Pop();
                SymbolStack.Pop();
            }
        }
    }

    private static (string? TargetType, string? TraitType) ExtractImplTypes(Node implNode)
    {
        string? traitType = null;
        string? targetType = null;

        var traitNode = implNode.GetChildForField(TreeSitterSyntax.Rust.Trait);
        if (traitNode.IsValid())
        {
            traitType = CleanTypeName(traitNode.Text);
        }

        var typeNode = implNode.GetChildForField(TreeSitterSyntax.Fields.Type);
        if (typeNode.IsValid())
        {
            targetType = CleanTypeName(typeNode.Text);
        }

        // Fallback: inspect children around "for" keyword if fields are absent
        if (string.IsNullOrEmpty(targetType))
        {
            var forIdx = -1;
            for (var i = 0; i < implNode.Children.Count; i++)
            {
                if (implNode.Children[i].Text == "for")
                {
                    forIdx = i;
                    break;
                }
            }

            if (forIdx > 0)
            {
                if (string.IsNullOrEmpty(traitType))
                {
                    var traitChild = implNode.Children.Take(forIdx)
                        .LastOrDefault(c => c.IsAny(TreeSitterSyntax.Rust.TypeIdentifier, TreeSitterSyntax.Rust.ScopedTypeIdentifier, TreeSitterSyntax.Rust.GenericType));
                    if (traitChild.IsValid())
                    {
                        traitType = CleanTypeName(traitChild.Text);
                    }
                }

                var targetChild = implNode.Children.Skip(forIdx + 1)
                    .FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Rust.TypeIdentifier, TreeSitterSyntax.Rust.ScopedTypeIdentifier, TreeSitterSyntax.Rust.GenericType));
                if (targetChild.IsValid())
                {
                    targetType = CleanTypeName(targetChild.Text);
                }
            }
            else
            {
                var targetChild = implNode.Children
                    .FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Rust.TypeIdentifier, TreeSitterSyntax.Rust.ScopedTypeIdentifier, TreeSitterSyntax.Rust.GenericType));
                if (targetChild.IsValid())
                {
                    targetType = CleanTypeName(targetChild.Text);
                }
            }
        }

        return (targetType, traitType);
    }

    private static string CleanTypeName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        var trimmed = raw.Trim();
        var genericIdx = trimmed.IndexOf('<');
        if (genericIdx > 0)
        {
            trimmed = trimmed[..genericIdx].Trim();
        }
        var sepIdx = trimmed.LastIndexOf("::", StringComparison.Ordinal);
        if (sepIdx >= 0)
        {
            trimmed = trimmed[(sepIdx + 2)..].Trim();
        }
        return trimmed;
    }

    protected override string? FindCallName(Node callNode)
    {
        if (callNode.Type == TreeSitterSyntax.Rust.MacroInvocation)
        {
            var macroChild = callNode.GetChildForField("macro") ??
                             callNode.Children.FirstOrDefault(c => c.Type is TreeSitterSyntax.Rust.Identifier or TreeSitterSyntax.Rust.ScopedIdentifier);
            if (macroChild.IsValid())
            {
                return macroChild.Text + "!";
            }
            return null;
        }

        if (callNode.Type != TreeSitterSyntax.Rust.CallExpression)
        {
            return null;
        }

        var func = callNode.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() && callNode.Children.Count > 0)
        {
            func = callNode.Children[0];
        }
        if (!func.IsValid()) return null;

        func = UnwrapTryAndAwait(func);

        if (func.Type == TreeSitterSyntax.Rust.GenericFunction)
        {
            var innerFunc = func.GetChildForField(TreeSitterSyntax.Fields.Function) ??
                            (func.Children.Count > 0 ? func.Children[0] : null);
            if (innerFunc.IsValid()) func = innerFunc;
        }

        if (func.Type == TreeSitterSyntax.Rust.FieldExpression)
        {
            var field = func.GetChildForField(TreeSitterSyntax.Fields.Field) ??
                        func.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.FieldIdentifier);
            if (field.IsValid())
            {
                return field.Text;
            }
        }

        if (func.Type == TreeSitterSyntax.Rust.ScopedIdentifier)
        {
            return func.Text;
        }

        if (func.Type == TreeSitterSyntax.Rust.Identifier)
        {
            return func.Text;
        }

        return null;
    }

    private static Node UnwrapTryAndAwait(Node node)
    {
        var current = node;
        while (current.IsValid())
        {
            if (current.Type == TreeSitterSyntax.Rust.TryExpression)
            {
                var inner = current.Children.FirstOrDefault(c => c.Text != "?");
                if (inner.IsValid() && inner.Id != current.Id)
                {
                    current = inner;
                    continue;
                }
            }
            else if (current.Type == TreeSitterSyntax.Rust.AwaitExpression)
            {
                var inner = current.Children.FirstOrDefault(c => c.Text != "await" && c.Text != ".");
                if (inner.IsValid() && inner.Id != current.Id)
                {
                    current = inner;
                    continue;
                }
            }
            break;
        }
        return current;
    }

    protected override void VisitStringLiteral(Node node, int depth)
    {
        var cleanText = CleanRustString(node.Text);
        if (!WorkspaceConventions.IsFrontendFilePath(RelativePath) && NestedSqlParser.TryParseSql(cleanText, out var firstWord, out var tableName))
        {
            var name = $"{firstWord} Query";
            VisitSymbolNode(node, OntologyConstants.NodeLabels.Query, name, depth, () =>
            {
                base.VisitStringLiteral(node, depth);
            });

            if (!string.IsNullOrEmpty(tableName))
            {
                var enclosingFunc = SymbolStack.FirstOrDefault(s => s.Kind == OntologyConstants.NodeLabels.Function);
                if (enclosingFunc != null)
                {
                    enclosingFunc.References.Add(new Reference("", tableName, OntologyConstants.Relationships.QueriesDb));
                }
            }
        }
        else
        {
            base.VisitStringLiteral(node, depth);
        }
    }

    protected override void VisitImportStatement(Node node, int depth)
    {
        var text = node.Text.Trim();
        if (text.StartsWith("use ", StringComparison.Ordinal) && text.EndsWith(';'))
        {
            var path = text["use ".Length..^1].Trim();
            var importType = RustModuleResolver.ResolveUsePath(path, RelativePath, _currentManifest);
            RawImports.Add(new RawImport(path, "", importType));
            ResolveAndInjectLibraryParser(path);
            ResolveAndInjectLibraryParser(path.Replace("::", "/"));
            var rootCrate = path.Split("::")[0].Trim();
            ResolveAndInjectLibraryParser(rootCrate);
        }

        VisitChildren(node, depth);
    }

    protected override void VisitVariableDeclaration(Node node, int depth)
    {
        if (node.Type == TreeSitterSyntax.Rust.LetDeclaration)
        {
            var patternNode = node.GetChildForField("pattern") ??
                              node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.Identifier);
            var valNode = node.GetChildForField(TreeSitterSyntax.Fields.Value);

            if (patternNode.IsValid())
            {
                var name = patternNode.Text;
                var initText = valNode.IsValid() ? valNode.Text : "";
                RawVariables.Add(new RawVariable(
                    name,
                    initText,
                    "local",
                    false,
                    "",
                    node.StartPosition.Row + 1,
                    node.EndPosition.Row + 1,
                    node.StartPosition.Column + 1,
                    node.EndPosition.Column + 1
                ));
            }
        }

        VisitChildren(node, depth);
    }

    private static string CleanRustString(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var trimmed = text.Trim();
        if (trimmed.StartsWith("r#\"", StringComparison.Ordinal) && trimmed.EndsWith("\"#", StringComparison.Ordinal) && trimmed.Length >= 5)
        {
            return trimmed[3..^2];
        }
        if (trimmed.StartsWith("r##\"", StringComparison.Ordinal) && trimmed.EndsWith("\"##", StringComparison.Ordinal) && trimmed.Length >= 7)
        {
            return trimmed[4..^3];
        }
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }
        return trimmed;
    }
}
