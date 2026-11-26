using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Parsers.Go.Libraries;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Go;

internal static class GoBindingExtractor
{
    internal const string GoInterfaceMethodMarker = "__go_iface_method__";
    private const string GoFieldResult = "result";
    private const string GoPropEnclosingType = "enclosing_type";
    private const string GoConstructorPrefix = "New";
    private const string GoHandlerFuncSuffix = "HandlerFunc";
    private const string GoPrefix = "go-";
    private const string WildcardSymbol = "*";
    private const string BlankIdentifier = "_";
    private const string DotImportAlias = ".";
    private const char PointerChar = '*';
    private const char DotChar = '.';
    private const char SlashChar = '/';
    private const char DashChar = '-';

    private static readonly HashSet<string> GoBuiltinTypes = new(StringComparer.Ordinal)
    {
        "bool", "byte", "complex64", "complex128", "error", "float32", "float64",
        "int", "int8", "int16", "int32", "int64", "comparable", "any",
        "rune", "string", "uint", "uint8", "uint16", "uint32", "uint64", "uintptr"
    };

    internal static void CollectGoImport(
        Node node,
        LanguageSyntaxProfile profile,
        IFileParser fileParser,
        string relativePath,
        string absoluteWorkspacePath,
        Func<string, string> cleanLiteral,
        Action<string> injectLibrary,
        List<RawImport> rawImports)
    {
        if (node.Children.Any(c =>
                c is ImportDeclSyntax ||
                c is SyntaxList { Kind: SyntaxListKind.Imports }))
        {
            return;
        }

        string? importText = null;
        string? explicitAlias = null;
        foreach (var child in node.Children)
        {
            if (!child.IsValid()) continue;
            if (child is IdentifierExprSyntax || child.Text is BlankIdentifier or DotImportAlias)
            {
                explicitAlias = child.Text.Trim();
            }
            else if (child is LiteralExprSyntax || profile.ImportIdentifierChildTypes.Contains(child.Type))
            {
                importText = cleanLiteral(child.Text);
            }
        }

        if (string.IsNullOrEmpty(importText) && node is ImportDeclSyntax { ModulePath.Length: > 0 } modDecl)
        {
            importText = cleanLiteral(modDecl.ModulePath);
        }

        if (string.IsNullOrEmpty(importText))
        {
            return;
        }

        var importType = fileParser.ResolveImportType(importText, relativePath, absoluteWorkspacePath);
        List<ImportSpecifier>? specifiers = null;
        if (explicitAlias == DotImportAlias)
        {
            specifiers = [new ImportSpecifier(WildcardSymbol, WildcardSymbol, IsDefault: false, IsNamespace: false)];
        }
        else if (explicitAlias != BlankIdentifier)
        {
            var localPkgName = !string.IsNullOrEmpty(explicitAlias) ? explicitAlias : InferGoDefaultPackageAlias(importText);
            if (!string.IsNullOrEmpty(localPkgName))
            {
                specifiers = [new ImportSpecifier(localPkgName, WildcardSymbol, IsDefault: false, IsNamespace: true)];
            }
        }

        rawImports.Add(new RawImport(importText, string.Empty, importType, specifiers));
        injectLibrary(importText);
    }

    internal static void CollectGoInterfaceMethods(
        Node ifaceOrTypeSpecNode,
        SyntacticSymbol ifaceSymbol,
        List<RawTypeBinding> rawTypeBindings)
    {
        var ifaceName = ifaceSymbol.Name;
        if (string.IsNullOrEmpty(ifaceName)) return;

        foreach (var desc in ifaceOrTypeSpecNode.DescendantsAndSelf())
        {
            if (!desc.IsValid()) continue;
            var hasParamList = desc.Children.Any(c => c is SyntaxList { Kind: SyntaxListKind.Parameters });
            if (!hasParamList) continue;

            var methodId = desc.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
            if (methodId != null && methodId.IsValid())
            {
                var mName = methodId.Text.Trim();
                if (!string.IsNullOrEmpty(mName))
                {
                    rawTypeBindings.Add(new RawTypeBinding(GoInterfaceMethodMarker, mName, string.Empty, ifaceName));
                }
            }
        }
    }

    internal static void CollectGoStructFields(
        Node structOrTypeSpecNode,
        SyntacticSymbol structSymbol,
        List<RawTypeBinding> rawTypeBindings,
        IReadOnlyList<RawImport>? fileImports = null)
    {
        var structName = structSymbol.Name;
        if (string.IsNullOrEmpty(structName)) return;

        foreach (var desc in structOrTypeSpecNode.DescendantsAndSelf())
        {
            if (desc is not MemberDeclSyntax { Kind: MemberSyntaxKind.Field }) continue;

            var fieldNames = new List<string>();
            Node? fieldTypeNode = null;
            foreach (var child in desc.Children)
            {
                if (!child.IsValid()) continue;
                if (child is IdentifierExprSyntax { Role: IdentifierRole.Member })
                {
                    fieldNames.Add(child.Text.Trim());
                }
                else if (IsGoTypeNode(child))
                {
                    fieldTypeNode = child;
                }
            }

            if (fieldTypeNode == null) continue;
            EmitGoUsesTypeReferences(fieldTypeNode, structSymbol, desc.StartPosition.Row + 1, desc.StartPosition.Column + 1, fileImports);

            var primaryType = ExtractPrimaryGoTypeName(fieldTypeNode, fileImports);
            if (string.IsNullOrEmpty(primaryType) || GoBuiltinTypes.Contains(primaryType)) continue;

            if (fieldNames.Count == 0)
            {
                var embeddedFieldName = primaryType.Contains(DotChar) ? primaryType[(primaryType.LastIndexOf(DotChar) + 1)..] : primaryType;
                rawTypeBindings.Add(new RawTypeBinding(embeddedFieldName, primaryType, string.Empty, structName));
                continue;
            }

            for (var i = 0; i < fieldNames.Count; i++)
            {
                rawTypeBindings.Add(new RawTypeBinding(fieldNames[i], primaryType, string.Empty, structName));
            }
        }
    }

    internal static void CollectGoFunctionSignature(
        Node fnNode,
        SyntacticSymbol fnSymbol,
        List<RawTypeBinding> rawTypeBindings,
        IReadOnlyList<RawImport>? fileImports = null)
    {
        var fnName = fnSymbol.Name;
        var paramLists = new List<Node>();
        foreach (var child in fnNode.Children)
        {
            if (child is SyntaxList { Kind: SyntaxListKind.Parameters })
            {
                paramLists.Add(child);
            }
        }

        var isMethod = fnNode is FunctionDeclSyntax { Kind: FunctionSyntaxKind.Method };
        var paramStartIdx = isMethod && paramLists.Count >= 1 ? 1 : 0;

        for (var p = paramStartIdx; p < paramLists.Count; p++)
        {
            CollectGoParameterListBindings(paramLists[p], fnSymbol, fnName, rawTypeBindings, fileImports);
        }

        if (isMethod && paramLists.Count >= 1)
        {
            CollectGoReceiverBinding(paramLists[0], fnSymbol, fnName, rawTypeBindings, fileImports);
        }

        var resultNode = fnNode.GetChildForField(GoFieldResult);
        if (resultNode.IsValid())
        {
            EmitGoUsesTypeReferences(resultNode, fnSymbol, resultNode.StartPosition.Row + 1, resultNode.StartPosition.Column + 1, fileImports);
        }
    }

    private static void CollectGoReceiverBinding(
        Node receiverParamList,
        SyntacticSymbol fnSymbol,
        string fnName,
        List<RawTypeBinding> rawTypeBindings,
        IReadOnlyList<RawImport>? fileImports)
    {
        foreach (var paramDecl in receiverParamList.Children)
        {
            if (paramDecl is not ParameterDeclSyntax) continue;

            string? recvVar = null;
            Node? recvTypeNode = null;
            foreach (var c in paramDecl.Children)
            {
                if (!c.IsValid()) continue;
                if (c is IdentifierExprSyntax { Role: not IdentifierRole.Type } && recvVar == null)
                {
                    recvVar = c.Text.Trim();
                }
                else if (IsGoTypeNode(c))
                {
                    recvTypeNode = c;
                }
            }

            if (recvTypeNode == null) continue;
            var recvTypeName = ExtractPrimaryGoTypeName(recvTypeNode, fileImports);
            if (string.IsNullOrEmpty(recvTypeName) || GoBuiltinTypes.Contains(recvTypeName)) continue;

            fnSymbol.Properties[GoPropEnclosingType] = recvTypeName;
            EmitGoUsesTypeReferences(recvTypeNode, fnSymbol, paramDecl.StartPosition.Row + 1, paramDecl.StartPosition.Column + 1, fileImports);
            if (!string.IsNullOrEmpty(recvVar))
            {
                rawTypeBindings.Add(new RawTypeBinding(recvVar, recvTypeName, string.Empty, fnName));
            }
        }
    }

    private static void CollectGoParameterListBindings(
        Node paramList,
        SyntacticSymbol fnSymbol,
        string fnName,
        List<RawTypeBinding> rawTypeBindings,
        IReadOnlyList<RawImport>? fileImports)
    {
        foreach (var paramDecl in paramList.Children)
        {
            if (paramDecl is not ParameterDeclSyntax) continue;

            var paramNames = new List<string>();
            Node? paramTypeNode = null;
            foreach (var c in paramDecl.Children)
            {
                if (!c.IsValid()) continue;
                if (c is IdentifierExprSyntax { Role: not IdentifierRole.Type })
                {
                    paramNames.Add(c.Text.Trim());
                }
                else if (IsGoTypeNode(c))
                {
                    paramTypeNode = c;
                }
            }

            if (paramTypeNode == null) continue;
            EmitGoUsesTypeReferences(paramTypeNode, fnSymbol, paramDecl.StartPosition.Row + 1, paramDecl.StartPosition.Column + 1, fileImports);

            var primaryType = ExtractPrimaryGoTypeName(paramTypeNode, fileImports);
            if (string.IsNullOrEmpty(primaryType) || GoBuiltinTypes.Contains(primaryType)) continue;

            for (var i = 0; i < paramNames.Count; i++)
            {
                rawTypeBindings.Add(new RawTypeBinding(paramNames[i], primaryType, string.Empty, fnName));
            }
        }
    }

    internal static void CollectGoCompositeLiteralTypeReference(
        Node compositeLitNode,
        SyntacticSymbol currentScope,
        IReadOnlyList<RawImport>? fileImports = null)
    {
        if (currentScope.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.File) return;

        var typeNode = compositeLitNode.GetChildForField(TreeSitterSyntax.Fields.Type);
        if (!typeNode.IsValid() && compositeLitNode.Children.Count > 0 && IsGoTypeNode(compositeLitNode.Children[0]))
        {
            typeNode = compositeLitNode.Children[0];
        }

        if (typeNode.IsValid())
        {
            EmitGoUsesTypeReferences(
                typeNode,
                currentScope,
                compositeLitNode.StartPosition.Row + 1,
                compositeLitNode.StartPosition.Column + 1,
                fileImports);
        }
    }

    internal static void TryCollectGoShortVarBindings(
        List<string> lhsNames,
        Node rightNode,
        string scopeName,
        List<RawTypeBinding> rawTypeBindings,
        IReadOnlyList<RawImport>? fileImports = null)
    {
        if (lhsNames.Count == 0 || !rightNode.IsValid()) return;

        var exprs = rightNode is SyntaxList { Kind: SyntaxListKind.Expressions }
            ? rightNode.Children.Where(c => c.IsValid()).ToList()
            : [rightNode];

        if (exprs.Count == 1 && exprs[0] is CallExprSyntax)
        {
            var inferred = InferTypeFromGoCall(exprs[0], fileImports);
            if (!string.IsNullOrEmpty(inferred) && !GoBuiltinTypes.Contains(inferred))
            {
                rawTypeBindings.Add(new RawTypeBinding(lhsNames[0], inferred, string.Empty, scopeName));
            }
            return;
        }

        var count = Math.Min(lhsNames.Count, exprs.Count);
        for (var i = 0; i < count; i++)
        {
            var inferred = InferTypeFromGoExpr(exprs[i], fileImports);
            if (!string.IsNullOrEmpty(inferred) && !GoBuiltinTypes.Contains(inferred))
            {
                rawTypeBindings.Add(new RawTypeBinding(lhsNames[i], inferred, string.Empty, scopeName));
            }
        }
    }

    private static string? InferTypeFromGoExpr(Node expr, IReadOnlyList<RawImport>? fileImports)
    {
        if (!expr.IsValid()) return null;
        if (expr is CallExprSyntax)
        {
            return InferTypeFromGoCall(expr, fileImports);
        }

        var target = expr;
        if (target is UnaryExprSyntax && target.Children.Count > 0)
        {
            target = target.Children[^1];
        }

        if (target is CollectionExprSyntax { Kind: CollectionSyntaxKind.Initializer })
        {
            var tNode = target.GetChildForField(TreeSitterSyntax.Fields.Type);
            if (!tNode.IsValid() && target.Children.Count > 0 && IsGoTypeNode(target.Children[0]))
            {
                tNode = target.Children[0];
            }
            return tNode.IsValid() ? ExtractPrimaryGoTypeName(tNode, fileImports) : null;
        }

        return null;
    }

    private static string? InferTypeFromGoCall(Node callExpr, IReadOnlyList<RawImport>? fileImports)
    {
        var fn = callExpr.GetFunctionNode();
        if (!fn.IsValid()) return null;

        string? pkg = null;
        string fnName;
        if (fn is MemberAccessExprSyntax mem)
        {
            var op = mem.Object ?? fn.GetChildForField(TreeSitterSyntax.Fields.Operand);
            var fld = fn.GetChildForField(TreeSitterSyntax.Fields.Field);
            pkg = op != null && op.IsValid() ? op.Text.Trim() : null;
            fnName = !string.IsNullOrEmpty(mem.MemberName) ? mem.MemberName : (fld.IsValid() ? fld.Text.Trim() : string.Empty);
            if (string.IsNullOrEmpty(fnName)) return null;
        }
        else
        {
            fnName = fn.Text.Trim();
        }

        if (fnName.StartsWith(GoConstructorPrefix, StringComparison.Ordinal) && fnName.Length > GoConstructorPrefix.Length)
        {
            var constructedType = fnName[GoConstructorPrefix.Length..];
            return !string.IsNullOrEmpty(pkg) ? $"{pkg}.{constructedType}" : constructedType;
        }

        if (fnName == GoConstructorPrefix && !string.IsNullOrEmpty(pkg) && char.IsLower(pkg[0]))
        {
            var inferredStruct = char.ToUpperInvariant(pkg[0]) + pkg[1..];
            return ClassifyGoPackageQualifier(pkg, fileImports) != null ? $"{pkg}.{inferredStruct}" : inferredStruct;
        }

        return null;
    }

    private static bool IsGoTypeNode(Node node) =>
        node is TypeRefSyntax or IdentifierExprSyntax { Role: IdentifierRole.Type } or TypeDeclSyntax;

    private static ImportType? ClassifyGoPackageQualifier(string pkgAlias, IReadOnlyList<RawImport>? fileImports)
    {
        if (string.IsNullOrEmpty(pkgAlias) || fileImports == null) return null;
        for (var i = 0; i < fileImports.Count; i++)
        {
            var specs = fileImports[i].Specifiers;
            if (specs == null) continue;
            for (var s = 0; s < specs.Count; s++)
            {
                if (string.Equals(specs[s].LocalName, pkgAlias, StringComparison.Ordinal))
                {
                    return fileImports[i].Type;
                }
            }
        }
        return null;
    }

    private static bool TryExtractQualifiedGoType(Node node, out string pkgAlias, out string typeName)
    {
        pkgAlias = string.Empty;
        typeName = string.Empty;
        if (node is not TypeRefSyntax { Kind: TypeRefKind.Named }) return false;

        foreach (var child in node.Children)
        {
            if (!child.IsValid()) continue;
            if (child is IdentifierExprSyntax { Role: IdentifierRole.Type } && string.IsNullOrEmpty(typeName))
            {
                typeName = child.Text.Trim();
            }
            else if (child is IdentifierExprSyntax { Role: not IdentifierRole.Type } && string.IsNullOrEmpty(pkgAlias))
            {
                pkgAlias = child.Text.Trim();
            }
        }

        return !string.IsNullOrEmpty(pkgAlias) && !string.IsNullOrEmpty(typeName);
    }

    private static string? ExtractPrimaryGoTypeName(Node typeNode, IReadOnlyList<RawImport>? fileImports = null)
    {
        var curr = typeNode;
        while (curr is TypeRefSyntax { Kind: TypeRefKind.Pointer or TypeRefKind.Slice or TypeRefKind.Array })
        {
            var next = curr.Children.FirstOrDefault(IsGoTypeNode);
            if (next == null || !next.IsValid()) break;
            curr = next;
        }

        if (TryExtractQualifiedGoType(curr, out var qPkg, out var qType))
        {
            return $"{qPkg}.{qType}";
        }

        if (curr is IdentifierExprSyntax { Role: IdentifierRole.Type })
        {
            return curr.Text.Trim();
        }

        var nestedQualified = curr.DescendantsAndSelf().LastOrDefault(d => d is TypeRefSyntax { Kind: TypeRefKind.Named });
        if (nestedQualified != null && TryExtractQualifiedGoType(nestedQualified, out var nPkg, out var nType))
        {
            return $"{nPkg}.{nType}";
        }

        var lastTypeId = curr.DescendantsAndSelf().LastOrDefault(d => d is IdentifierExprSyntax { Role: IdentifierRole.Type });
        if (lastTypeId != null && lastTypeId.IsValid())
        {
            return lastTypeId.Text.Trim();
        }

        if (curr is TypeRefSyntax)
        {
            var raw = curr.Text.Trim().TrimStart(PointerChar, '[', ']');
            var dot = raw.LastIndexOf(DotChar);
            if (dot > 0)
            {
                var pkg = raw[..dot].Trim();
                var shortName = raw[(dot + 1)..].Trim();
                return ClassifyGoPackageQualifier(pkg, fileImports) != null ? $"{pkg}.{shortName}" : shortName;
            }
            return raw;
        }

        return null;
    }

    private static void EmitGoUsesTypeReferences(
        Node typeTree,
        SyntacticSymbol targetSymbol,
        int line,
        int col,
        IReadOnlyList<RawImport>? fileImports = null)
    {
        EmitGoUsesTypeNodeRecursive(typeTree, targetSymbol, line, col, fileImports);
    }

    private static void EmitGoUsesTypeNodeRecursive(
        Node current,
        SyntacticSymbol targetSymbol,
        int line,
        int col,
        IReadOnlyList<RawImport>? fileImports)
    {
        if (!current.IsValid()) return;

        if (TryExtractQualifiedGoType(current, out var pkgAlias, out var shortType))
        {
            if (GoBuiltinTypes.Contains(shortType)) return;
            var pkgKind = ClassifyGoPackageQualifier(pkgAlias, fileImports);
            if (pkgKind == ImportType.External)
            {
                return;
            }

            var refTarget = $"{pkgAlias}.{shortType}";
            AddUniqueUsesTypeReference(targetSymbol, refTarget, line, col);
            return;
        }

        if (current is IdentifierExprSyntax { Role: IdentifierRole.Type })
        {
            var tName = current.Text.Trim();
            if (!string.IsNullOrEmpty(tName) && !GoBuiltinTypes.Contains(tName))
            {
                AddUniqueUsesTypeReference(targetSymbol, tName, line, col);
            }
            return;
        }

        foreach (var child in current.Children)
        {
            EmitGoUsesTypeNodeRecursive(child, targetSymbol, line, col, fileImports);
        }
    }

    private static void AddUniqueUsesTypeReference(SyntacticSymbol targetSymbol, string targetName, int line, int col)
    {
        if (!targetSymbol.References.Any(r =>
                r.Kind == CodeExplorer.Cypher.Common.RelationshipKind.UsesType &&
                string.Equals(r.TargetName, targetName, StringComparison.Ordinal)))
        {
            targetSymbol.References.Add(new Reference(
                string.Empty,
                targetName,
                CodeExplorer.Cypher.Common.RelationshipKind.UsesType,
                line,
                col));
        }
    }

    private static string? InferGoDefaultPackageAlias(string importPath)
    {
        var trimmed = importPath.Trim().Trim(SlashChar);
        if (string.IsNullOrEmpty(trimmed)) return null;

        var parts = trimmed.Split(SlashChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var last = parts[^1];
        if (parts.Length >= 2 && last.Length >= 2 && last[0] == 'v' && last[1..].All(char.IsDigit))
        {
            last = parts[^2];
        }

        var dashIdx = last.LastIndexOf(DashChar);
        if (dashIdx > 0 && dashIdx < last.Length - 1 && last.StartsWith(GoPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return last[(dashIdx + 1)..];
        }

        return last;
    }

    internal static string? ExtractGoRouteHandlerTarget(Node handlerArg)
    {
        if (!handlerArg.IsValid() || handlerArg is LambdaExprSyntax)
        {
            return null;
        }

        if (handlerArg is IdentifierExprSyntax)
        {
            return handlerArg.Text.Trim();
        }

        if (handlerArg is MemberAccessExprSyntax mem)
        {
            if (!string.IsNullOrEmpty(mem.MemberName)) return mem.MemberName.Trim();
            var field = handlerArg.GetChildForField(TreeSitterSyntax.Fields.Field);
            return field.IsValid() ? field.Text.Trim() : null;
        }

        if (handlerArg is CallExprSyntax)
        {
            var callArgs = GoAstHelper.GetCallArguments(handlerArg);
            for (var i = callArgs.Count - 1; i >= 0; i--)
            {
                var argCandidate = callArgs[i];
                if (argCandidate is LambdaExprSyntax)
                {
                    return null;
                }
                if (argCandidate is MemberAccessExprSyntax innerMem)
                {
                    if (!string.IsNullOrEmpty(innerMem.MemberName)) return innerMem.MemberName.Trim();
                    var field = argCandidate.GetChildForField(TreeSitterSyntax.Fields.Field);
                    if (field.IsValid()) return field.Text.Trim();
                }
                else if (argCandidate is IdentifierExprSyntax)
                {
                    var idText = argCandidate.Text.Trim();
                    if (idText is not ("ctx" or "mux" or "r" or "router"))
                    {
                        return idText;
                    }
                }
                else if (argCandidate is CallExprSyntax)
                {
                    var nested = ExtractGoRouteHandlerTarget(argCandidate);
                    if (!string.IsNullOrEmpty(nested)) return nested;
                }
            }

            var outerFn = handlerArg.GetFunctionNode();
            if (outerFn.IsValid())
            {
                var outerName = ExtractSimpleCallableName(outerFn);
                if (!string.IsNullOrEmpty(outerName) &&
                    !outerName.EndsWith(GoHandlerFuncSuffix, StringComparison.Ordinal))
                {
                    return outerName;
                }
            }
        }

        return null;
    }

    private static string? ExtractSimpleCallableName(Node node)
    {
        if (node is MemberAccessExprSyntax mem)
        {
            if (!string.IsNullOrEmpty(mem.MemberName)) return mem.MemberName;
            var f = node.GetChildForField(TreeSitterSyntax.Fields.Field);
            return f.IsValid() ? f.Text : node.Text;
        }
        if (node is IdentifierExprSyntax)
        {
            return node.Text;
        }
        return null;
    }
}
