using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Parser.Layers;

internal static class Layer3ScopeVisibilityPass
{
    private const string ExprArrow = "=>";
    private const string GetterToken = "get;";
    private const string SetterToken = "set;";

    private static readonly string[] MutableCollectionTypeTokens =
    [
        "ConcurrentDictionary", "Dictionary<", "List<", "HashSet<",
        "ConcurrentQueue", "ConcurrentBag", "ConcurrentStack",
        "Queue<", "Stack<", "LinkedList<",
        "Map<", "Set<", "new Map(", "new Set("
    ];

    internal static void EnrichMemberDeclarationsFromDocument(
        SyntaxDocument doc,
        Dictionary<(int Line, string Name), MemberNode> membersByLineAndName)
    {
        if (membersByLineAndName.Count == 0) return;

        foreach (var memDecl in doc.AllMembers)
        {
            string? attrsStr = null;
            if (memDecl.Annotations.Count > 0)
            {
                var attrNames = new List<string>(memDecl.Annotations.Count);
                for (var i = 0; i < memDecl.Annotations.Count; i++)
                {
                    var aName = memDecl.Annotations[i].Name.Trim().TrimStart('@', '#', '[').TrimEnd(']');
                    var pIdx = aName.IndexOf('(');
                    if (pIdx > 0) aName = aName[..pIdx].Trim();
                    if (!string.IsNullOrEmpty(aName) && !attrNames.Contains(aName, StringComparer.Ordinal))
                    {
                        attrNames.Add(aName);
                    }
                }
                if (attrNames.Count > 0)
                {
                    attrsStr = string.Join(", ", attrNames);
                }
            }

            var isStatic = (memDecl.Modifiers & SyntaxModifiers.Static) != 0 ||
                           HasDeclarationModifierKeyword(memDecl, isStaticCheck: true);
            var isConstOrReadOnly = (memDecl.Modifiers & (SyntaxModifiers.Const | SyntaxModifiers.ReadOnly)) != 0 ||
                                    memDecl.Kind == MemberSyntaxKind.Constant ||
                                    HasDeclarationModifierKeyword(memDecl, isStaticCheck: false) ||
                                    IsGetOnlyPropertyDeclaration(memDecl);

            void ApplyToMember(MemberNode target)
            {
                if (attrsStr != null) target.Attributes ??= attrsStr;
                if (isStatic && target.MemberKind != MemberKind.Parameter) target.IsStatic = true;
                if (IsMutableCollectionMember(target, memDecl))
                {
                    target.IsConstant = false;
                }
                else if (isConstOrReadOnly)
                {
                    target.IsConstant = true;
                }
            }

            var startLine = memDecl.Span.StartLine + 1;
            if (!string.IsNullOrEmpty(memDecl.Name) && membersByLineAndName.TryGetValue((startLine, memDecl.Name), out var directMem))
            {
                ApplyToMember(directMem);
            }
            else
            {
                foreach (var child in memDecl.Children)
                {
                    if (child is VariableDeclSyntax vd)
                    {
                        if (!string.IsNullOrEmpty(vd.Name) && membersByLineAndName.TryGetValue((vd.Span.StartLine + 1, vd.Name), out var vdMem))
                        {
                            ApplyToMember(vdMem);
                        }
                        foreach (var sub in vd.Children)
                        {
                            if (sub is VariableDeclSyntax subVd && !string.IsNullOrEmpty(subVd.Name) &&
                                membersByLineAndName.TryGetValue((subVd.Span.StartLine + 1, subVd.Name), out var subMem))
                            {
                                ApplyToMember(subMem);
                            }
                        }
                    }
                }
            }
        }
    }

    private static bool HasDeclarationModifierKeyword(SyntaxNode decl, bool isStaticCheck)
    {
        var span = decl.AsSpan();
        var eqIdx = span.IndexOf('=');
        if (eqIdx > 0) span = span[..eqIdx];
        var braceIdx = span.IndexOf('{');
        if (braceIdx > 0) span = span[..braceIdx];

        while (!span.IsEmpty)
        {
            span = span.TrimStart();
            if (span.IsEmpty) break;
            var idx = 0;
            while (idx < span.Length && (char.IsLetterOrDigit(span[idx]) || span[idx] == '_')) idx++;
            if (idx == 0)
            {
                span = span[1..];
                continue;
            }
            var token = span[..idx];
            if (isStaticCheck)
            {
                if (token is "static") return true;
            }
            else
            {
                if (token is "const" or "readonly" or "final") return true;
            }
            span = span[idx..];
        }
        return false;
    }

    private static bool IsGetOnlyPropertyDeclaration(MemberDeclSyntax memDecl)
    {
        if (memDecl.Kind != MemberSyntaxKind.Property) return false;
        var span = memDecl.AsSpan();
        if (span.Contains(ExprArrow.AsSpan(), StringComparison.Ordinal)) return true;
        return span.Contains(GetterToken.AsSpan(), StringComparison.Ordinal) &&
               !span.Contains(SetterToken.AsSpan(), StringComparison.Ordinal);
    }

    private static bool IsMutableCollectionMember(MemberNode target, MemberDeclSyntax memDecl)
    {
        var dt = target.DataType;
        var init = target.InitializerExpression;
        var declSpan = memDecl.AsSpan();

        for (var i = 0; i < MutableCollectionTypeTokens.Length; i++)
        {
            var token = MutableCollectionTypeTokens[i].AsSpan();
            if ((!string.IsNullOrEmpty(dt) && dt.AsSpan().Contains(token, StringComparison.Ordinal)) ||
                (!string.IsNullOrEmpty(init) && init.AsSpan().Contains(token, StringComparison.Ordinal)) ||
                declSpan.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    internal static void EnrichNonTreeSitterTree(SyntaxTree syntaxTree, string? projectName, bool isTest, SymbolResolutionBuffer? symbols)
    {
        var effectiveIsTest = isTest || (syntaxTree.FileNode?.IsTest ?? false) || WorkspaceConventions.IsTestFilePath(syntaxTree.RelativePath);
        if (effectiveIsTest && syntaxTree.FileNode != null)
        {
            static void PropagateTestFlag(IOntologyNode parent)
            {
                for (var i = 0; i < parent.Children.Count; i++)
                {
                    var child = parent.Children[i];
                    if (child is TypeNode tn) tn.IsTest = true;
                    else if (child is FunctionNode fn) fn.IsTest = true;
                    if (child.Children.Count > 0) PropagateTestFlag(child);
                }
            }
            syntaxTree.FileNode.IsTest = true;
            PropagateTestFlag(syntaxTree.FileNode);
        }
        if (syntaxTree.RawImports.Count > 0)
        {
            RegisterFileRawImports(symbols, syntaxTree.RelativePath, syntaxTree.RawImports);
        }
        if (syntaxTree.FileNode != null && syntaxTree.Document != null)
        {
            PopulateSymbolScopeMetadata(
                syntaxTree.FileNode,
                syntaxTree.Document,
                syntaxTree.RelativePath,
                projectName ?? syntaxTree.FileNode.ProjectName,
                effectiveIsTest,
                symbols);
        }
    }

    internal static void RegisterFileRawImports(SymbolResolutionBuffer? symbols, string relativePath, List<RawImport> rawImports)
    {
        if (symbols == null || rawImports.Count == 0) return;
        var normRelPath = relativePath.Replace('\\', '/');
        for (var r = 0; r < rawImports.Count; r++)
        {
            var ri = rawImports[r];
            if (!string.IsNullOrWhiteSpace(ri.Path))
            {
                var nsSet = symbols.FileImportedNamespaces.GetOrAdd(normRelPath, _ => new HashSet<string>(StringComparer.Ordinal));
                lock (nsSet)
                {
                    nsSet.Add(ri.Path);
                }
            }
            if (ri.Specifiers != null)
            {
                for (var s = 0; s < ri.Specifiers.Count; s++)
                {
                    var spec = ri.Specifiers[s];
                    symbols.RegisterKnownCallableName(spec.LocalName);
                    symbols.RegisterKnownCallableName(spec.ImportedName);
                    symbols.RegisterKnownTypeName(spec.LocalName);
                    symbols.RegisterKnownTypeName(spec.ImportedName);
                }
            }
        }
    }

    internal static void RegisterSyntaxTreeWorkspaceSymbols(
        SyntaxTree syntaxTree,
        SymbolResolutionBuffer symbols)
    {
        var normRelPath = syntaxTree.RelativePath.Replace('\\', '/');
        var doc = syntaxTree.Document;
        if (doc != null)
        {
            if (!string.IsNullOrWhiteSpace(doc.PackageOrNamespace))
            {
                symbols.FileNamespaces[normRelPath] = doc.PackageOrNamespace;
            }

            for (var m = 0; m < doc.Imports.Count; m++)
            {
                var imp = doc.Imports[m];
                if (!string.IsNullOrWhiteSpace(imp.ModulePath))
                {
                    var nsSet = symbols.FileImportedNamespaces.GetOrAdd(normRelPath, _ => new HashSet<string>(StringComparer.Ordinal));
                    lock (nsSet)
                    {
                        nsSet.Add(imp.ModulePath);
                    }
                }
                if (!string.IsNullOrWhiteSpace(imp.Alias))
                {
                    symbols.RegisterKnownCallableName(imp.Alias);
                    symbols.RegisterKnownTypeName(imp.Alias);
                }
            }

            for (var f = 0; f < doc.AllFunctions.Count; f++)
            {
                var fnDecl = doc.AllFunctions[f];
                if (fnDecl.Kind == FunctionSyntaxKind.Accessor) continue;
                var fnName = Layer3SyntacticParser.ExtractSyntaxFunctionName(fnDecl);
                if (!fnName.StartsWith("<anon_", StringComparison.Ordinal))
                {
                    symbols.RegisterKnownCallableName(fnName);
                }
            }

            for (var t = 0; t < doc.AllTypes.Count; t++)
            {
                var td = doc.AllTypes[t];
                var tName = !string.IsNullOrWhiteSpace(td.Name)
                    ? td.Name
                    : (td.GetChildForField(SyntaxFields.Name) ?? td.FirstChildOfType<IdentifierExprSyntax>())?.Text.Trim();
                if (!string.IsNullOrWhiteSpace(tName))
                {
                    symbols.RegisterKnownTypeName(tName);
                }
            }
        }

        if (syntaxTree.FileNode != null && syntaxTree.FileNode.Children.Count > 0)
        {
            RegisterOntologyTreeSymbolNames(syntaxTree.FileNode, symbols);
        }
    }

    private static void RegisterOntologyTreeSymbolNames(IOntologyNode parent, SymbolResolutionBuffer symbols)
    {
        for (var i = 0; i < parent.Children.Count; i++)
        {
            var child = parent.Children[i];
            if (child is TypeNode tn)
            {
                symbols.RegisterKnownTypeName(tn.Name);
            }
            else if (child is FunctionNode fn)
            {
                symbols.RegisterKnownCallableName(fn.Name);
            }
            if (child.Children.Count > 0)
            {
                RegisterOntologyTreeSymbolNames(child, symbols);
            }
        }
    }

    internal static void PopulateSymbolScopeMetadata(
        FileNode fileNode,
        SyntaxDocument doc,
        string relativePath,
        string? projectName,
        bool isTest,
        SymbolResolutionBuffer? symbols)
    {
        var hasLateExportMarkers = doc.SourceText.Contains("module.exports", StringComparison.Ordinal) ||
                                   doc.SourceText.Contains("exports.", StringComparison.Ordinal) ||
                                   doc.SourceText.Contains("export {", StringComparison.Ordinal) ||
                                   doc.SourceText.Contains("export default ", StringComparison.Ordinal);

        var normFilePath = relativePath.Replace('\\', '/');
        var fileNamespace = !string.IsNullOrWhiteSpace(doc.PackageOrNamespace) ? doc.PackageOrNamespace : null;
        if (symbols != null && fileNamespace != null)
        {
            symbols.FileNamespaces[normFilePath] = fileNamespace;
        }

        var fnDeclByLineAndName = new Dictionary<(int Line, string Name), FunctionDeclSyntax>();
        var typeDeclByName = new Dictionary<string, TypeDeclSyntax>(StringComparer.Ordinal);
        var hasModuleExports = hasLateExportMarkers;

        for (var i = 0; i < doc.Imports.Count; i++)
        {
            if (doc.Imports[i].IsReExport)
            {
                hasModuleExports = true;
                break;
            }
        }

        for (var i = 0; i < doc.AllTypes.Count; i++)
        {
            var td = doc.AllTypes[i];
            if ((td.Modifiers & SyntaxModifiers.Export) != 0) hasModuleExports = true;
            var tName = !string.IsNullOrWhiteSpace(td.Name)
                ? td.Name
                : (td.GetChildForField(SyntaxFields.Name) ?? td.FirstChildOfType<IdentifierExprSyntax>())?.Text.Trim();
            if (!string.IsNullOrWhiteSpace(tName))
            {
                typeDeclByName.TryAdd(tName, td);
            }
        }

        for (var i = 0; i < doc.AllFunctions.Count; i++)
        {
            var fd = doc.AllFunctions[i];
            if ((fd.Modifiers & SyntaxModifiers.Export) != 0) hasModuleExports = true;
            var fnName = Layer3SyntacticParser.ExtractSyntaxFunctionName(fd);
            fnDeclByLineAndName.TryAdd((fd.Span.StartLine + 1, fnName), fd);
        }

        void WalkScopeTree(IOntologyNode node, TypeNode? enclosingType, FunctionNode? enclosingFunction)
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (child is TypeNode tn)
                {
                    typeDeclByName.TryGetValue(tn.Name, out var td);
                    var typeVis = DetermineTypeVisibility(tn, td, doc.LanguageName, enclosingType != null, hasModuleExports);
                    if (typeVis == SymbolVisibility.Public && tn.IsExported == null)
                    {
                        tn.IsExported = true;
                    }
                    symbols?.RegisterKnownTypeName(tn.Name);
                    symbols?.SymbolScopes.TryAdd(tn.Id, new SymbolScopeInfo(
                        tn.Id,
                        normFilePath,
                        projectName ?? tn.ProjectName,
                        fileNamespace,
                        enclosingType?.Name,
                        typeVis,
                        isTest || tn.IsTest));

                    WalkScopeTree(tn, tn, null);
                }
                else if (child is FunctionNode fn)
                {
                    fnDeclByLineAndName.TryGetValue((fn.StartLine, fn.Name), out var fd);
                    var fnVis = DetermineFunctionVisibility(fn, fd, doc.LanguageName, enclosingType, enclosingFunction != null, hasModuleExports);
                    if (string.IsNullOrEmpty(fn.Visibility))
                    {
                        fn.Visibility = fnVis switch
                        {
                            SymbolVisibility.Private or SymbolVisibility.FilePrivate => "private",
                            SymbolVisibility.Protected => "protected",
                            SymbolVisibility.Internal => "internal",
                            _ => "public"
                        };
                    }
                    symbols?.RegisterKnownCallableName(fn.Name);
                    symbols?.SymbolScopes.TryAdd(fn.Id, new SymbolScopeInfo(
                        fn.Id,
                        normFilePath,
                        projectName ?? fn.ProjectName,
                        fileNamespace,
                        enclosingType?.Name ?? fn.TryGetSignals()?.EnclosingTypeName,
                        fnVis,
                        isTest || fn.IsTest));

                    WalkScopeTree(fn, enclosingType, fn);
                }
            }
        }

        WalkScopeTree(fileNode, null, null);
    }

    private static SymbolVisibility DetermineTypeVisibility(
        TypeNode tn,
        TypeDeclSyntax? td,
        string languageName,
        bool isNestedType,
        bool hasModuleExports)
    {
        if (tn.IsExported == true || tn.Flags.HasFlag(SymbolFlags.IsExported))
        {
            return SymbolVisibility.Public;
        }

        if (td != null)
        {
            if ((td.Modifiers & (SyntaxModifiers.Public | SyntaxModifiers.Export)) != 0)
                return SymbolVisibility.Public;
            if ((td.Modifiers & SyntaxModifiers.Private) != 0)
                return isNestedType ? SymbolVisibility.Private : SymbolVisibility.FilePrivate;
            if ((td.Modifiers & SyntaxModifiers.Internal) != 0)
                return SymbolVisibility.Internal;
            if ((td.Modifiers & SyntaxModifiers.Protected) != 0)
                return SymbolVisibility.Protected;
        }

        return languageName switch
        {
            "csharp" => isNestedType ? SymbolVisibility.Private : SymbolVisibility.Internal,
            "go" => tn.Name.Length > 0 && char.IsAsciiLetterLower(tn.Name[0]) ? SymbolVisibility.Internal : SymbolVisibility.Public,
            "typescript" or "tsx" or "javascript" => hasModuleExports ? SymbolVisibility.FilePrivate : SymbolVisibility.Public,
            "java" => SymbolVisibility.Internal,
            _ => SymbolVisibility.Public
        };
    }

    private static SymbolVisibility DetermineFunctionVisibility(
        FunctionNode fn,
        FunctionDeclSyntax? fd,
        string languageName,
        TypeNode? enclosingType,
        bool isNestedFunction,
        bool hasModuleExports)
    {
        if (fn.IsModuleInit == true || string.Equals(fn.Name, "<module_init>", StringComparison.Ordinal))
        {
            return SymbolVisibility.FilePrivate;
        }

        if (isNestedFunction)
        {
            return SymbolVisibility.FilePrivate;
        }

        if (fd != null)
        {
            if (Layer3SyntacticParser.FindEnclosingSyntaxFunction(fd) != null)
            {
                return SymbolVisibility.FilePrivate;
            }
            if ((fd.Modifiers & (SyntaxModifiers.Public | SyntaxModifiers.Export)) != 0)
                return SymbolVisibility.Public;
            if ((fd.Modifiers & SyntaxModifiers.Internal) != 0)
                return SymbolVisibility.Internal;
            if ((fd.Modifiers & SyntaxModifiers.Protected) != 0)
                return SymbolVisibility.Protected;
            if ((fd.Modifiers & SyntaxModifiers.Private) != 0)
                return enclosingType != null ? SymbolVisibility.Private : SymbolVisibility.FilePrivate;
        }

        if (!string.IsNullOrEmpty(fn.Visibility))
        {
            if (fn.Visibility is "pub" or "public") return SymbolVisibility.Public;
            if (fn.Visibility is "pub(crate)" or "internal") return SymbolVisibility.Internal;
            if (fn.Visibility is "protected") return SymbolVisibility.Protected;
            if (fn.Visibility is "private")
            {
                return languageName == "rust"
                    ? SymbolVisibility.Internal
                    : (enclosingType != null ? SymbolVisibility.Private : SymbolVisibility.FilePrivate);
            }
        }

        if (enclosingType == null && (fn.IsExported == true || fn.Flags.HasFlag(SymbolFlags.IsExported)))
        {
            return SymbolVisibility.Public;
        }

        return languageName switch
        {
            "csharp" => enclosingType != null
                ? (enclosingType.TypeKind == TypeKind.Interface ? SymbolVisibility.Public : SymbolVisibility.Private)
                : SymbolVisibility.Private,
            "go" => fn.Name.Length > 0 && char.IsAsciiLetterLower(fn.Name[0]) ? SymbolVisibility.Internal : SymbolVisibility.Public,
            "rust" => SymbolVisibility.Internal,
            "typescript" or "tsx" or "javascript" => enclosingType != null
                ? (fn.Name.StartsWith('#') ? SymbolVisibility.Private : SymbolVisibility.Public)
                : (hasModuleExports ? SymbolVisibility.FilePrivate : SymbolVisibility.Public),
            "python" => fn.Name.StartsWith("__", StringComparison.Ordinal) && !fn.Name.EndsWith("__", StringComparison.Ordinal)
                ? (enclosingType != null ? SymbolVisibility.Private : SymbolVisibility.FilePrivate)
                : SymbolVisibility.Public,
            "java" => enclosingType?.TypeKind == TypeKind.Interface ? SymbolVisibility.Public : SymbolVisibility.Internal,
            _ => SymbolVisibility.Public
        };
    }
}
