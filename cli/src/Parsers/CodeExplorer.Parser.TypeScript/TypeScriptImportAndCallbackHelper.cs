using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;
using CodeExplorer.Parser.TypeScript.Libraries;

namespace CodeExplorer.Parser.TypeScript;

internal static class TypeScriptImportAndCallbackHelper
{
    public const string RequireSyntheticPrefix = "__req_";
    public const string SelfAliasModulePath = "__self__";
    public const string CallbackArgContext = "callback_arg";

    private const string ExportStatementType = "export_statement";
    private const string ExportClauseType = "export_clause";
    private const string ExportSpecifierType = "export_specifier";
    private const string ImportClauseType = "import_clause";
    private const string NamespaceImportType = "namespace_import";
    private const string NamedImportsType = "named_imports";
    private const string ImportSpecifierType = "import_specifier";
    private const string ImportRequireClauseType = "import_require_clause";
    private const string ObjectPatternType = "object_pattern";
    private const string ShorthandPropertyIdentifierPatternType = "shorthand_property_identifier_pattern";
    private const string PairPatternType = "pair_pattern";
    private const string ParenthesizedExpressionType = "parenthesized_expression";
    private const string WildcardSymbol = "*";
    private const string DefaultExportName = "default";
    private const string AliasFieldName = "alias";

    private static readonly HashSet<string> IgnoredCallbackIdentifiers = new(StringComparer.Ordinal)
    {
        "true", "false", "null", "undefined", "this", "arguments", "window", "document",
        "global", "globalThis", "process", "console", "Math", "JSON", "Object", "Array",
        "String", "Number", "Boolean", "Promise", "Error", "Symbol", "Map", "Set", "Date", "RegExp",
        "req", "res", "next", "err", "error", "ctx", "event", "e", "item", "x", "y", "acc", "val"
    };

    public static void ExtractCommonJsRequireImport(
        Node callNode,
        List<RawImport> rawImports,
        Action<string> resolveLibrary)
    {
        var argList = callNode.GetField(TreeSitterSyntax.Fields.Arguments);
        if (!argList.IsValid() || argList.Children.Count == 0)
        {
            return;
        }

        var firstArg = argList.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.String));
        if (!firstArg.IsValid())
        {
            return;
        }

        var importPath = firstArg.Text.Trim('\'', '"');
        if (string.IsNullOrEmpty(importPath))
        {
            return;
        }

        var specifiers = new List<ImportSpecifier>();
        var parent = callNode.Parent;
        while (parent.IsValid() && parent.Is(ParenthesizedExpressionType))
        {
            parent = parent.Parent;
        }

        if (parent.IsValid() && parent.Is(TreeSitterSyntax.TypeScript.VariableDeclarator))
        {
            PopulateRequireDeclaratorSpecifiers(parent, specifiers);
        }
        else if (parent.IsValid() && parent.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var propNode = parent.GetField(TreeSitterSyntax.Fields.Property);
            var memberName = propNode.IsValid() ? propNode.Text : null;
            var grandParent = parent.Parent;

            if (!string.IsNullOrEmpty(memberName))
            {
                if (grandParent.IsValid() && grandParent.Is(TreeSitterSyntax.TypeScript.VariableDeclarator))
                {
                    var nameField = grandParent.GetField(TreeSitterSyntax.Fields.Name);
                    if (nameField.IsValid() && nameField.Is(TreeSitterSyntax.TypeScript.Identifier))
                    {
                        specifiers.Add(new ImportSpecifier(nameField.Text, memberName));
                    }
                }
                else if (grandParent.IsValid() && grandParent.Is(TreeSitterSyntax.TypeScript.CallExpression))
                {
                    var syntheticAlias = $"{RequireSyntheticPrefix}{importPath}";
                    specifiers.Add(new ImportSpecifier(syntheticAlias, WildcardSymbol, IsNamespace: true));
                }
            }
        }
        else if (parent.IsValid() && parent.Is(TreeSitterSyntax.TypeScript.AssignmentExpression))
        {
            var leftNode = parent.GetField(TreeSitterSyntax.Fields.Left);
            var leftText = leftNode.IsValid() ? leftNode.Text.Trim() : string.Empty;
            if (leftText is "module.exports" or "exports")
            {
                specifiers.Add(new ImportSpecifier(WildcardSymbol, WildcardSymbol, IsNamespace: true));
            }
            else if (leftNode.IsValid() && leftNode.Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                specifiers.Add(new ImportSpecifier(leftText, DefaultExportName, IsDefault: true));
            }
        }

        rawImports.Add(new RawImport(importPath, string.Empty, ImportType.External, specifiers));
        resolveLibrary(importPath);
    }

    private static void PopulateRequireDeclaratorSpecifiers(Node declarator, List<ImportSpecifier> specifiers)
    {
        var nameField = declarator.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameField.IsValid())
        {
            return;
        }

        if (nameField.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            specifiers.Add(new ImportSpecifier(nameField.Text, DefaultExportName, IsDefault: true));
            return;
        }

        if (!nameField.Is(ObjectPatternType))
        {
            return;
        }

        foreach (var child in nameField.Children)
        {
            if (child.Is(ShorthandPropertyIdentifierPatternType))
            {
                var idText = child.Text.Trim();
                if (!string.IsNullOrEmpty(idText))
                {
                    specifiers.Add(new ImportSpecifier(idText, idText));
                }
            }
            else if (child.Is(PairPatternType))
            {
                var keyNode = child.GetField(TreeSitterSyntax.Fields.Key);
                var valNode = child.GetField(TreeSitterSyntax.Fields.Value);
                if (!keyNode.IsValid() || !valNode.IsValid())
                {
                    var named = child.Children.Where(c => c.IsNamed).ToList();
                    if (named.Count >= 2)
                    {
                        keyNode = named[0];
                        valNode = named[1];
                    }
                }

                if (keyNode.IsValid() && valNode.IsValid())
                {
                    var importedName = keyNode.Text.Trim().Trim('\'', '"');
                    var localName = valNode.Text.Trim();
                    if (!string.IsNullOrEmpty(importedName) && !string.IsNullOrEmpty(localName))
                    {
                        specifiers.Add(new ImportSpecifier(localName, importedName));
                    }
                }
            }
        }
    }

    public static string? TryExtractDirectRequireMemberCall(Node unwrappedObj, string memberName)
    {
        if (!unwrappedObj.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            return null;
        }

        var fnNode = unwrappedObj.GetField(TreeSitterSyntax.Fields.Function);
        if (!fnNode.IsValid() || fnNode.Text != "require")
        {
            return null;
        }

        var argList = unwrappedObj.GetField(TreeSitterSyntax.Fields.Arguments);
        if (!argList.IsValid() || argList.Children.Count == 0)
        {
            return null;
        }

        var firstArg = argList.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.String));
        if (!firstArg.IsValid())
        {
            return null;
        }

        var importPath = firstArg.Text.Trim('\'', '"');
        return string.IsNullOrEmpty(importPath) ? null : $"{RequireSyntheticPrefix}{importPath}.{memberName}";
    }

    public static void CollectImportOrExportStatement(
        Node node,
        List<RawImport> rawImports,
        Action<string> resolveLibrary)
    {
        var sourceNode = node.GetField(TreeSitterSyntax.Fields.Source);
        if (!sourceNode.IsValid())
        {
            sourceNode = node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.String));
        }

        if (sourceNode.IsValid())
        {
            var importPath = sourceNode.Text.Trim('\'', '"');
            var specifiers = ExtractImportSpecifiers(node);
            rawImports.Add(new RawImport(importPath, string.Empty, ImportType.External, specifiers));
            resolveLibrary(importPath);
            return;
        }

        if (node.Is(ExportStatementType))
        {
            var specifiers = ExtractImportSpecifiers(node);
            var aliased = specifiers
                .Where(s => !string.IsNullOrEmpty(s.ImportedName) && !string.Equals(s.LocalName, s.ImportedName, StringComparison.Ordinal))
                .ToList();
            if (aliased.Count > 0)
            {
                rawImports.Add(new RawImport(SelfAliasModulePath, string.Empty, ImportType.Internal, aliased));
            }
        }
    }

    public static void CollectCommonJsExportAliases(Node assignNode, List<RawImport> rawImports)
    {
        var leftNode = assignNode.GetField(TreeSitterSyntax.Fields.Left);
        var rightNode = assignNode.GetField(TreeSitterSyntax.Fields.Right);
        if (!leftNode.IsValid() || !rightNode.IsValid())
        {
            return;
        }

        var leftText = leftNode.Text.Trim();
        var specifiers = new List<ImportSpecifier>();

        if ((leftText.StartsWith("exports.", StringComparison.Ordinal) ||
             leftText.StartsWith("module.exports.", StringComparison.Ordinal)) &&
            rightNode.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var exportedName = leftText[(leftText.LastIndexOf('.') + 1)..].Trim();
            var internalName = rightNode.Text.Trim();
            if (!string.IsNullOrEmpty(exportedName) && !string.IsNullOrEmpty(internalName) &&
                !string.Equals(exportedName, internalName, StringComparison.Ordinal))
            {
                specifiers.Add(new ImportSpecifier(exportedName, internalName));
            }
        }
        else if (leftText is "module.exports" or "exports" && rightNode.Is(TreeSitterSyntax.TypeScript.Object))
        {
            foreach (var pair in rightNode.FindChildrenOfType(TreeSitterSyntax.TypeScript.Pair))
            {
                var keyNode = pair.GetField(TreeSitterSyntax.Fields.Key);
                var valNode = pair.GetField(TreeSitterSyntax.Fields.Value);
                if (keyNode.IsValid() && valNode.IsValid() && valNode.Is(TreeSitterSyntax.TypeScript.Identifier))
                {
                    var exportedName = keyNode.Text.Trim().Trim('\'', '"', '`');
                    var internalName = valNode.Text.Trim();
                    if (!string.IsNullOrEmpty(exportedName) && !string.IsNullOrEmpty(internalName) &&
                        !string.Equals(exportedName, internalName, StringComparison.Ordinal))
                    {
                        specifiers.Add(new ImportSpecifier(exportedName, internalName));
                    }
                }
            }
        }

        if (specifiers.Count > 0)
        {
            rawImports.Add(new RawImport(SelfAliasModulePath, string.Empty, ImportType.Internal, specifiers));
        }
    }

    public static void CollectCallbackArgumentReferences(Node callNode, SyntacticSymbol currentScope)
    {
        if (currentScope.Kind == CodeExplorer.Cypher.Common.SyntacticSymbolKind.File)
        {
            return;
        }

        var fnNode = callNode.GetFunctionNode();
        if (fnNode.IsValid() && fnNode.Text == "require")
        {
            return;
        }

        var argsNode = callNode.GetField(TreeSitterSyntax.Fields.Arguments);
        if (!argsNode.IsValid() || argsNode.Children.Count == 0)
        {
            return;
        }

        foreach (var rawArg in argsNode.Children)
        {
            var arg = rawArg is CodeExplorer.Core.Parser.Syntax.ArgumentSyntax argSyn && argSyn.Value != null
                ? argSyn.Value
                : rawArg;
            if (!arg.IsValid() || !arg.Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                continue;
            }

            var cbName = arg.Text.Trim();
            if (string.IsNullOrEmpty(cbName) || IgnoredCallbackIdentifiers.Contains(cbName))
            {
                continue;
            }

            var line = (int)arg.StartPosition.Row + 1;
            var col = (int)arg.StartPosition.Column + 1;
            if (!currentScope.References.Any(r => r.TargetName == cbName && r.Line == line && r.Kind == OntologyConstants.Relationships.Calls))
            {
                currentScope.References.Add(new Reference(
                    string.Empty,
                    cbName,
                    OntologyConstants.Relationships.Calls,
                    line,
                    col,
                    argFlow: CallbackArgContext));
            }
        }
    }

    public static List<ImportSpecifier> ExtractImportSpecifiers(Node node)
    {
        var specifiers = new List<ImportSpecifier>();

        var clause = node.FindChildOfType(ImportClauseType);
        if (clause.IsValid())
        {
            PopulateImportClauseSpecifiers(clause, specifiers);
            return specifiers;
        }

        var reqClause = node.FindChildOfType(ImportRequireClauseType);
        if (reqClause.IsValid())
        {
            var id = reqClause.FindChildOfType(TreeSitterSyntax.TypeScript.Identifier);
            if (id.IsValid())
            {
                specifiers.Add(new ImportSpecifier(id.Text, DefaultExportName, IsDefault: true));
            }
            return specifiers;
        }

        var exportClause = node.FindChildOfType(ExportClauseType);
        if (exportClause.IsValid())
        {
            PopulateSpecifierList(exportClause, ExportSpecifierType, specifiers);
            return specifiers;
        }

        if (node is CodeExplorer.Core.Parser.Syntax.ImportDeclSyntax { IsWildcard: true } || node.Children.Any(c => c.Type == "*"))
        {
            specifiers.Add(new ImportSpecifier(WildcardSymbol, WildcardSymbol, IsNamespace: true));
        }

        return specifiers;
    }

    private static void PopulateImportClauseSpecifiers(Node clause, List<ImportSpecifier> specifiers)
    {
        foreach (var child in clause.Children)
        {
            if (child.Is(TreeSitterSyntax.TypeScript.Identifier))
            {
                specifiers.Add(new ImportSpecifier(child.Text, DefaultExportName, IsDefault: true));
            }
            else if (child.Is(NamespaceImportType))
            {
                var id = child.FindChildOfType(TreeSitterSyntax.TypeScript.Identifier);
                if (id.IsValid())
                {
                    specifiers.Add(new ImportSpecifier(id.Text, WildcardSymbol, IsNamespace: true));
                }
            }
            else if (child.Is(NamedImportsType))
            {
                PopulateSpecifierList(child, ImportSpecifierType, specifiers);
            }
        }
    }

    private static void PopulateSpecifierList(Node container, string specifierNodeType, List<ImportSpecifier> specifiers)
    {
        foreach (var spec in container.FindChildrenOfType(specifierNodeType))
        {
            var nameNode = spec.GetField(TreeSitterSyntax.Fields.Name);
            var aliasNode = spec.GetField(AliasFieldName);

            if (nameNode.IsValid())
            {
                var impName = nameNode.Text;
                var locName = aliasNode.IsValid() ? aliasNode.Text : impName;
                specifiers.Add(new ImportSpecifier(locName, impName));
            }
            else
            {
                var idents = spec.FindChildrenOfType(TreeSitterSyntax.TypeScript.Identifier).ToList();
                if (idents.Count == 1)
                {
                    specifiers.Add(new ImportSpecifier(idents[0].Text, idents[0].Text));
                }
                else if (idents.Count >= 2)
                {
                    specifiers.Add(new ImportSpecifier(idents[1].Text, idents[0].Text));
                }
            }
        }
    }
}
