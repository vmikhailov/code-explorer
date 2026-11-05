using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer3SyntacticParser
{
    private static bool HasTopLevelCallsOrStatements(SyntaxDocument doc)
    {
        if (doc.TopLevelStatements.Count > 0) return true;
        foreach (var call in doc.AllCalls)
        {
            var curr = call.Parent;
            var isEnclosed = false;
            while (curr != null)
            {
                if (curr is FunctionDeclSyntax or TypeDeclSyntax)
                {
                    isEnclosed = true;
                    break;
                }
                curr = curr.Parent;
            }
            if (!isEnclosed) return true;
        }
        return false;
    }

    private static void AttachParameterMemberNodes(
        FunctionNode fnNode,
        List<string> paramNames,
        string workspaceId,
        string relativePath)
    {
        for (var i = 0; i < paramNames.Count; i++)
        {
            var pName = paramNames[i];
            if (string.IsNullOrEmpty(pName)) continue;
            if (fnNode.Children.OfType<MemberNode>().Any(m => m.MemberKind == CodeExplorer.Cypher.Common.MemberKind.Parameter && m.Name == pName))
            {
                continue;
            }

            var paramId = $"{fnNode.Id}:param:{i}:{pName}";
            var paramNode = new MemberNode(
                paramId,
                pName,
                paramId,
                relativePath,
                relativePath,
                fnNode.StartLine,
                fnNode.StartLine,
                1,
                1,
                CodeExplorer.Cypher.Common.MemberKind.Parameter,
                null)
            {
                ParameterIndex = i,
                VariableClassification = "parameter"
            };
            fnNode.Children.Add(paramNode);
        }
    }

    private static string ExtractSyntaxFunctionName(FunctionDeclSyntax fnDecl)
    {
        if (!string.IsNullOrWhiteSpace(fnDecl.Name)) return fnDecl.Name;
        var nameNode = fnDecl.GetChildForField(SyntaxFields.Name) ??
                       fnDecl.FirstChildOfType<IdentifierExprSyntax>();
        if (nameNode != null)
        {
            var span = nameNode.AsSpan().Trim();
            if (!span.IsEmpty) return SyntaxStringPool.Intern(span);
        }
        return $"<anon_{fnDecl.Span.StartLine + 1}>";
    }

    private static FunctionDeclSyntax? FindEnclosingSyntaxFunction(SyntaxNode node)
    {
        var curr = node.Parent;
        while (curr != null)
        {
            if (curr is FunctionDeclSyntax fn) return fn;
            curr = curr.Parent;
        }
        return null;
    }

    private static List<string> ExtractSyntaxParameterNames(SyntaxNode fnOrLambda)
    {
        var names = new List<string>();
        if (fnOrLambda is FunctionDeclSyntax { Parameters.Count: > 0 } fnDecl)
        {
            foreach (var p in fnDecl.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(p.Name))
                {
                    names.Add(p.Name);
                }
                else
                {
                    CollectDestructuredParameterIdentifiers(p, names);
                }
            }
            if (names.Count > 0) return names;
        }

        var paramsContainer = fnOrLambda.GetChildForField(SyntaxFields.Parameters) ??
                              fnOrLambda.GetChildForField("parameter") ??
                              fnOrLambda.NamedChildren.FirstOrDefault(c => c.RawGrammarKind is "formal_parameters" or "parameters" or "parameter_list");
        if (paramsContainer != null)
        {
            if (paramsContainer is IdentifierExprSyntax singleId)
            {
                var t = singleId.Text.Trim();
                if (!string.IsNullOrEmpty(t)) names.Add(t);
                return names;
            }
            foreach (var p in paramsContainer.NamedChildren)
            {
                CollectDestructuredParameterIdentifiers(p, names);
            }
        }
        return names;
    }

    private static void CollectDestructuredParameterIdentifiers(SyntaxNode node, List<string> names)
    {
        if (node is ParameterDeclSyntax { Name.Length: > 0 } pd)
        {
            if (!names.Contains(pd.Name, StringComparer.Ordinal)) names.Add(pd.Name);
            return;
        }
        if (node is IdentifierExprSyntax or IdentifierPatternSyntax)
        {
            if (node.Parent?.RawGrammarKind == "pair_pattern" && ReferenceEquals(node.Parent.GetChildForField(SyntaxFields.Key), node))
            {
                return;
            }
            var span = node.AsSpan().Trim().TrimStart('.');
            if (!span.IsEmpty && span is not ("this" or "self"))
            {
                var txt = SyntaxStringPool.Intern(span);
                if (!names.Contains(txt, StringComparer.Ordinal))
                {
                    names.Add(txt);
                }
            }
            return;
        }
        if (node.RawGrammarKind == "shorthand_property_identifier_pattern")
        {
            var span = node.AsSpan().Trim();
            if (!span.IsEmpty)
            {
                var txt = SyntaxStringPool.Intern(span);
                if (!names.Contains(txt, StringComparer.Ordinal))
                {
                    names.Add(txt);
                }
            }
            return;
        }
        if (node is AssignmentExprSyntax assignPat)
        {
            var left = assignPat.Left ?? assignPat.GetChildForField(SyntaxFields.Left) ?? (assignPat.NamedChildren.Count > 0 ? assignPat.NamedChildren[0] : null);
            if (left != null) CollectDestructuredParameterIdentifiers(left, names);
            return;
        }
        for (var i = 0; i < node.NamedChildren.Count; i++)
        {
            var child = node.NamedChildren[i];
            if (child is TypeRefSyntax) continue;
            CollectDestructuredParameterIdentifiers(child, names);
        }
    }

    private static void CollectReturnedClosureParameters(FunctionDeclSyntax fnDecl, List<string> paramNames)
    {
        var stack = new Stack<SyntaxNode>();
        stack.Push(fnDecl);
        while (stack.Count > 0)
        {
            var curr = stack.Pop();
            if (curr is FunctionDeclSyntax && !ReferenceEquals(curr, fnDecl)) continue;
            if (curr is ReturnOrYieldStmtSyntax retStmt)
            {
                var retVal = retStmt.Value ?? retStmt.GetChildForField(SyntaxFields.Value) ?? retStmt.NamedChildren.FirstOrDefault();
                if (retVal is LambdaExprSyntax retLambda)
                {
                    foreach (var innerParam in ExtractSyntaxParameterNames(retLambda))
                    {
                        if (!string.IsNullOrEmpty(innerParam) && !paramNames.Contains(innerParam, StringComparer.Ordinal))
                        {
                            paramNames.Add(innerParam);
                        }
                    }
                }
            }
            for (var i = curr.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(curr.Children[i]);
            }
        }
    }

    private static IEnumerable<AssignmentExprSyntax> EnumerateSyntaxAssignments(SyntaxNode root)
    {
        var stack = new Stack<SyntaxNode>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var curr = stack.Pop();
            if (curr is AssignmentExprSyntax assign) yield return assign;
            for (var i = curr.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(curr.Children[i]);
            }
        }
    }

    private static bool TryParseSyntaxPrototypeMethod(
        AssignmentExprSyntax assignExpr,
        out string className,
        out string methodName,
        out LambdaExprSyntax lambdaNode)
    {
        className = string.Empty;
        methodName = string.Empty;
        lambdaNode = null!;

        var left = assignExpr.Left ?? assignExpr.GetChildForField(SyntaxFields.Left) ?? (assignExpr.NamedChildren.Count > 0 ? assignExpr.NamedChildren[0] : null);
        var right = assignExpr.Right ?? assignExpr.GetChildForField(SyntaxFields.Right) ?? (assignExpr.NamedChildren.Count > 1 ? assignExpr.NamedChildren[1] : null);
        if (left is not MemberAccessExprSyntax outerMem || right is not LambdaExprSyntax rhsLambda) return false;

        var innerObj = outerMem.Object ?? outerMem.GetChildForField(SyntaxFields.Object) ?? (outerMem.NamedChildren.Count > 0 ? outerMem.NamedChildren[0] : null);
        if (innerObj is not MemberAccessExprSyntax innerMem) return false;

        var innerPropSpan = !innerMem.MemberNameSpan.IsEmpty
            ? innerMem.MemberNameSpan
            : (innerMem.GetChildForField(SyntaxFields.Property) ?? (innerMem.NamedChildren.Count > 1 ? innerMem.NamedChildren[^1] : null)) is { } ipNode ? ipNode.AsSpan().Trim() : ReadOnlySpan<char>.Empty;
        if (innerPropSpan is not "prototype") return false;

        var outerPropSpan = !outerMem.MemberNameSpan.IsEmpty
            ? outerMem.MemberNameSpan
            : (outerMem.GetChildForField(SyntaxFields.Property) ?? (outerMem.NamedChildren.Count > 1 ? outerMem.NamedChildren[^1] : null)) is { } opNode ? opNode.AsSpan().Trim() : ReadOnlySpan<char>.Empty;
        if (outerPropSpan.IsEmpty) return false;

        var clsNode = innerMem.Object ?? innerMem.GetChildForField(SyntaxFields.Object) ?? (innerMem.NamedChildren.Count > 0 ? innerMem.NamedChildren[0] : null);
        if (clsNode == null) return false;
        var clsSpan = clsNode.AsSpan().Trim();
        if (clsSpan.IsEmpty) return false;

        className = SyntaxStringPool.Intern(clsSpan);
        methodName = SyntaxStringPool.Intern(outerPropSpan);
        lambdaNode = rhsLambda;
        return true;
    }

    private static bool IsSyntaxFunctionExported(FunctionDeclSyntax fnDecl, SyntaxDocument doc)
    {
        if ((fnDecl.Modifiers & SyntaxModifiers.Export) != 0 || (fnDecl.Modifiers & SyntaxModifiers.Public) != 0)
        {
            return true;
        }

        var curr = fnDecl.Parent;
        while (curr != null)
        {
            if (curr.RawGrammarKind is "export_statement" or "export_declaration") return true;
            if (curr is TypeDeclSyntax td)
            {
                if ((td.Modifiers & SyntaxModifiers.Export) != 0 || (td.Modifiers & SyntaxModifiers.Public) != 0) return true;
                if (td.Parent?.RawGrammarKind is "export_statement" or "export_declaration") return true;
                var typeName = !string.IsNullOrWhiteSpace(td.Name)
                    ? td.Name
                    : (td.GetChildForField(SyntaxFields.Name) ?? td.NamedChildren.FirstOrDefault(c => c is IdentifierExprSyntax))?.Text.Trim();
                if (!string.IsNullOrEmpty(typeName) && doc.SourceText.Contains(typeName, StringComparison.Ordinal) &&
                    (doc.SourceText.Contains("module.exports", StringComparison.Ordinal) || doc.SourceText.Contains("exports.", StringComparison.Ordinal)))
                {
                    return true;
                }
            }
            curr = curr.Parent;
        }

        var fnName = ExtractSyntaxFunctionName(fnDecl);
        if (!string.IsNullOrEmpty(fnName) && doc.SourceText.Contains(fnName, StringComparison.Ordinal))
        {
            if (doc.SourceText.Contains("module.exports", StringComparison.Ordinal) ||
                doc.SourceText.Contains($"exports.{fnName}", StringComparison.Ordinal) ||
                doc.SourceText.Contains("export {", StringComparison.Ordinal) ||
                doc.SourceText.Contains($"export default {fnName}", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void RecordDocumentDiagnostics(SyntaxTree syntaxTree, FileNode fileNode, ParsingContext ctx)
    {
        var diags = syntaxTree.Document?.Diagnostics;
        if (diags == null || diags.Count == 0) return;

        fileNode.HasParseIssues = true;
        fileNode.ParseIssueCount = diags.Count;
        fileNode.Flags |= SymbolFlags.HasParseIssues;

        var lang = syntaxTree.Document?.LanguageName ?? syntaxTree.FileParser.LanguageName;
        for (var d = 0; d < diags.Count; d++)
        {
            var diag = diags[d];
            ctx.RecordScanIssue(
                stage: "Layer3_Syntactic",
                category: CodeExplorer.Cypher.Common.TaxonomyEnumExtendedExtensions.ToCanonicalString(diag.Kind),
                relativeOrFullPath: fileNode.Path,
                message: diag.Message,
                name: "SyntaxError",
                severity: "warning",
                startLine: diag.Span.StartLine + 1,
                endLine: diag.Span.EndLine + 1,
                startCol: diag.Span.StartColumn + 1,
                endCol: diag.Span.EndColumn + 1,
                language: lang,
                parentNodeId: fileNode.Id,
                isFileParent: true,
                logToConsole: d == 0);
        }
    }

    private static readonly Regex ConfigRegex = new(
        @"(?i)(config|settings?|cfg|\benv\b|db_?conn|\burl\b|\buri\b|\bport\b|\bhost\b|user(name)?|pass(word)?|token|secret|\bkey\b|auth|api_?key|connection_?string)",
        RegexOptions.Compiled
    );

    private static readonly Regex ConfigInitializerRegex = new(
        @"(?i)(process\.env|Configuration\[|Environment\.GetEnvironmentVariable|System\.Environment|import\.meta\.env|dotenv|require\(['""]dotenv['""]\))",
        RegexOptions.Compiled
    );

    private static readonly Regex EtlRegex = new(
        @"(?i)(\betl\b|\bsql\b|\bquery\b|\bselect\b|\binsert\b|\bupsert\b|\bschema\b|\btable\b|\bcolumn\b|\bdatabase\b|\bmigration\b|\bextract\b|\btransform\b|\bload\b)",
        RegexOptions.Compiled
    );

    private static readonly Regex SqlQueryRegex = new(
        @"(?i)^[\s@$""'\`]*\s*(select|insert|update|delete|create\s+table|drop\s+table|merge|alter\s+table)\b",
        RegexOptions.Compiled
    );

    private static void PopulateMemberNodesFromRawVariables(
        FileNode fileNode,
        IReadOnlyList<RawVariable> fileVariables,
        string workspaceId,
        string relativePath)
    {
        foreach (var rawVar in fileVariables)
        {
            var isConfig = (rawVar.Name.Length >= 3 && ConfigRegex.IsMatch(rawVar.Name)) ||
                           (rawVar.InitializerText.Length >= 6 && ConfigInitializerRegex.IsMatch(rawVar.InitializerText));
            var isEtl = (rawVar.Name.Length >= 3 && EtlRegex.IsMatch(rawVar.Name)) ||
                        (rawVar.InitializerText.Length >= 6 && SqlQueryRegex.IsMatch(rawVar.InitializerText));
            // ConfigInitializerRegex and EtlRegex handled above
            // SqlQueryRegex handled above
            var isConstant = rawVar.IsConstant;
            var isGlobal = rawVar.Scope == "global";
            var isProperty = rawVar.MemberKind == CodeExplorer.Cypher.Common.MemberKind.Property;
            var isField = rawVar.MemberKind == CodeExplorer.Cypher.Common.MemberKind.Field;

            if (isConfig || isEtl || isConstant || isGlobal || isProperty || isField)
            {
                var varTypeStr = new List<string>();
                if (isConfig) varTypeStr.Add("config");
                if (isEtl) varTypeStr.Add("etl");
                if (isConstant) varTypeStr.Add("constant");
                if (isGlobal) varTypeStr.Add("global");
                if (isProperty) varTypeStr.Add("property");
                if (isField) varTypeStr.Add("field");

                var varType = string.Join(",", varTypeStr);
                var varId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Member:{rawVar.Name}";

                var varNode = new MemberNode(
                    varId,
                    rawVar.Name,
                    varId,
                    fileNode.FullPath,
                    varId,
                    rawVar.StartLine,
                    rawVar.EndLine,
                    rawVar.StartCol,
                    rawVar.EndCol,
                    rawVar.MemberKind ?? CodeExplorer.Cypher.Common.MemberKind.Variable,
                    string.IsNullOrEmpty(rawVar.TypeName) ? null : rawVar.TypeName
                )
                {
                    VariableClassification = varType,
                    InitializerExpression = rawVar.InitializerText,
                    IsConstant = isConstant
                };

                TryInsertVariable(fileNode, varNode, rawVar.StartLine);
            }
        }
    }

    private static bool TryInsertVariable(IOntologyNode parentNode, MemberNode varNode, int line)
    {
        foreach (var child in parentNode.Children)
        {
            if (child is TypeNode cn && line >= cn.StartLine && line <= cn.EndLine)
            {
                if (TryInsertVariable(cn, varNode, line)) return true;
            }

            if (child is FunctionNode fn && line >= fn.StartLine && line <= fn.EndLine)
            {
                if (TryInsertVariable(fn, varNode, line)) return true;
            }
        }

        if (parentNode.Children.All(c => c.Id != varNode.Id && !(c is MemberNode existingMember && existingMember.Name == varNode.Name)))
        {
            parentNode.Children.Add(varNode);
        }
        return true;
    }

    private static IOntologyNode? MapSyntacticSymbolToOntology(
        SyntacticSymbol syntactic,
        string fileName,
        string relativePath,
        string workspaceId,
        string parentScopeId,
        ParsingContext? ctx = null,
        int overloadIndex = 0,
        string? projectName = null,
        bool isTest = false,
        ProjectEntityKind? projectKind = null,
        SyntaxTree? syntaxTree = null,
        Dictionary<(CodeExplorer.Cypher.Common.SyntacticSymbolKind Kind, string Name), int>? fileSymbolCounts = null)
    {
        var node = syntactic.Node;
        var kind = CodeExplorer.Cypher.Common.TaxonomyEnumExtendedExtensions.ToCanonicalString(syntactic.Kind);
        var name = syntactic.Name;

        var symbolIsTest = isTest ||
            (syntactic.TryGetProperty(OntologyConstants.PropertyNames.IsTest, out var propIsTest) && bool.TryParse(propIsTest, out var b) && b);
        var stageOrder = syntactic.TryGetProperty(OntologyConstants.PropertyNames.StageOrder, out var so) && int.TryParse(so, out var parsedOrder) ? parsedOrder : (int?)null;

        var isType = kind is "Class" or "Interface" or "Struct" or "Enum" or OntologyConstants.NodeLabels.Type;
        var mappedKind = isType ? "Type" : kind;
        var idSuffix = overloadIndex > 1 ? $"#{overloadIndex}" : "";
        var symbolId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:{mappedKind}:{name}{idSuffix}";

        IOntologyNode? typedNode = null;
        if (kind is "Class" or "Struct" or "Enum" || kind == OntologyConstants.NodeLabels.Type)
        {
            var typeKind = syntactic.TryGetProperty(OntologyConstants.PropertyNames.TypeKind, out var tk)
                ? tk
                : (kind == OntologyConstants.NodeLabels.Type ? "class" : kind.ToLowerInvariant());
            typedNode = new TypeNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1, typeKind,
                symbolIsTest, projectKind, projectName, stageOrder);
        }
        else if (kind == "Interface")
        {
            typedNode = new TypeNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1, "interface",
                symbolIsTest, projectKind, projectName, stageOrder);
        }
        else if (kind == OntologyConstants.NodeLabels.Function)
        {
            typedNode = new FunctionNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1,
                symbolIsTest, projectKind, projectName, stageOrder);
        }
        else if (kind == "Member" || kind == OntologyConstants.NodeLabels.Member)
        {
            var memberKind = syntactic.TryGetProperty(OntologyConstants.PropertyNames.MemberKind, out var mk) ? mk : "field";
            var dataType = syntactic.TryGetProperty(OntologyConstants.PropertyNames.DataType, out var dt) ? dt : null;
            typedNode = new MemberNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1,
                memberKind, dataType);
        }
        else if (kind == OntologyConstants.NodeLabels.CallLiteral)
        {
            typedNode = CreateCallLiteralNode(name, node, relativePath, workspaceId, syntactic, symbolIsTest, projectKind, projectName);
        }
        else if (kind == OntologyConstants.NodeLabels.CatchClause)
        {
            typedNode = CreateCatchClauseNode(name, node, relativePath, workspaceId, syntactic, symbolIsTest, projectKind, projectName);
        }
        else
        {
            // Semantic symbols (EntryPoint, ExternalService, Table, Topic, Query)
            // are collected on the syntaxTree for Layer 4 Semantic Analysis
            syntactic.ParentScopeId = parentScopeId;
            syntaxTree?.SemanticSymbols.Add(syntactic);
            return null;
        }

        if (typedNode == null) return null;

        var childCounts = fileSymbolCounts ?? new Dictionary<(CodeExplorer.Cypher.Common.SyntacticSymbolKind Kind, string Name), int>();
        foreach (var childSyntactic in syntactic.Children)
        {
            if (syntactic.TryGetProperty(OntologyConstants.PropertyNames.GraphqlField, out var parentGqlField))
            {
                childSyntactic.Properties.TryAdd(OntologyConstants.PropertyNames.GraphqlField, parentGqlField);
            }
            if (syntactic.TryGetProperty(OntologyConstants.PropertyNames.GraphqlSchema, out var parentGqlSchema))
            {
                childSyntactic.Properties.TryAdd(OntologyConstants.PropertyNames.GraphqlSchema, parentGqlSchema);
            }

            var key = (childSyntactic.Kind, childSyntactic.Name);
            childCounts.TryGetValue(key, out var count);
            count++;
            childCounts[key] = count;

            var childNode = MapSyntacticSymbolToOntology(childSyntactic, fileName, relativePath, workspaceId, symbolId, ctx, count > 1 ? count : 0, projectName, symbolIsTest, projectKind, syntaxTree, childCounts);
            if (childNode != null)
            {
                typedNode.Children.Add(childNode);
            }
        }

        foreach (var reference in syntactic.References)
        {
            var target = reference.TargetName;
            var resolvedScopeId = string.IsNullOrEmpty(reference.ScopeSymbolId) ? typedNode.Id : reference.ScopeSymbolId;
            typedNode.References.Add(reference with { ScopeSymbolId = resolvedScopeId, TargetName = target });
        }

        if (syntactic.HasProperties)
        {
            foreach (var (k, v) in syntactic.Properties)
            {
                if (k is "value" or "callee" or "arg_index" or "file_path" or "line" or "column" or "role" or "is_decorator"
                    or "exception_type" or "is_empty" or "has_comment" or "statement_count"
                    or "member_kind" or "data_type" or "type_kind" or "stage_order")
                {
                    continue;
                }
                typedNode.SetExtension(k, v);
            }
        }

        return typedNode;
    }

    private static CallLiteralNode CreateCallLiteralNode(
        string name,
        SyntaxNode node,
        string relativePath,
        string workspaceId,
        SyntacticSymbol syntactic,
        bool symbolIsTest,
        ProjectEntityKind? projectKind,
        string? projectName)
    {
        var val = syntactic.Text ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Value) ?? syntactic.Name;
        var callee = syntactic.Callee ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Callee) ?? "";
        var argIdx = syntactic.ArgIndex != 0 ? syntactic.ArgIndex : (int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.ArgIndex), out var ai) ? ai : 0);
        var line = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Line), out var ln) ? ln : node.StartPosition.Row + 1;
        var col = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Column), out var cl) ? cl : node.StartPosition.Column + 1;
        var role = syntactic.Role ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Role) ?? "business";
        var isDecorator = syntactic.IsDecorator || (bool.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.IsDecorator), out var isDec) && isDec);
        var litId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CallLiteral}:{relativePath}:{line}:{col}";

        return new CallLiteralNode(litId, name, val, callee, argIdx, relativePath, line, col, symbolIsTest, projectKind, projectName, role, isDecorator);
    }

    private static CatchClauseNode CreateCatchClauseNode(
        string name,
        SyntaxNode node,
        string relativePath,
        string workspaceId,
        SyntacticSymbol syntactic,
        bool symbolIsTest,
        ProjectEntityKind? projectKind,
        string? projectName)
    {
        var exType = syntactic.RequestType ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.ExceptionType) ?? "";
        var isEmpty = syntactic.IsEmptyCatch || (bool.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.IsEmpty), out var ie) && ie);
        var hasComment = syntactic.HasCatchComment || (bool.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.HasComment), out var hc) && hc);
        var stmtCount = syntactic.ArgIndex != 0 ? syntactic.ArgIndex : (int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.StatementCount), out var sc) ? sc : 0);
        var line = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Line), out var ln) ? ln : node.StartPosition.Row + 1;
        var col = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Column), out var cl) ? cl : node.StartPosition.Column + 1;
        var catchId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CatchClause}:{relativePath}:{line}:{col}";

        return new CatchClauseNode(catchId, name, exType, relativePath, line, col, isEmpty, hasComment, stmtCount, symbolIsTest, projectKind, projectName);
    }

    public static bool IsRouteOrConfigCandidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/').ToLowerInvariant();
        var ext = Path.GetExtension(normalized);
        if (ext is not (".ts" or ".js" or ".cs" or ".go" or ".py" or ".json"))
            return false;

        var fileName = Path.GetFileName(normalized);
        if (fileName.Contains("route") || fileName.Contains("const") || fileName.Contains("config") ||
            fileName.Contains("api") || fileName.Contains("endpoint") || fileName.Contains("url") ||
            fileName.Contains("env") || fileName.Contains("client") || fileName.Contains("service") ||
            fileName.Contains("importer") || fileName.Contains("catalog") || fileName.Contains("gateway") ||
            fileName.Contains("http") || fileName.StartsWith("main.") || fileName.StartsWith("app.") ||
            fileName.StartsWith("bootstrap.") || fileName.StartsWith("index.") ||
            fileName.StartsWith("program.") || fileName.StartsWith("startup."))
        {
            return true;
        }

        // Dedicated route / configuration / constants directory segments
        return normalized.Contains("/routes/") || normalized.Contains("/route/") ||
               normalized.Contains("/config/") || normalized.Contains("/configs/") ||
               normalized.Contains("/constants/") || normalized.Contains("/endpoints/") ||
               normalized.Contains("/gateways/");
    }

    private static List<Relationship> ResolveCrossProjectSharedFileContains(
        IReadOnlyList<RawImport> rawImports,
        Dictionary<string, ProjectNode> primaryOwnerByFilePath,
        Dictionary<string, FileNode> fileNodeByPath,
        List<Relationship> existingProjectRels)
    {
        var newRels = new List<Relationship>();
        if (rawImports.Count == 0 || fileNodeByPath.Count == 0) return newRels;

        var localImportEdges = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var imp in rawImports)
        {
            if (string.IsNullOrWhiteSpace(imp.FilePath) || string.IsNullOrWhiteSpace(imp.Path)) continue;
            if (!imp.Path.StartsWith('.')) continue;

            var fromNorm = imp.FilePath.Replace('\\', '/').Trim('/');
            var resolvedTarget = TryResolveRelativeFileImport(fromNorm, imp.Path, fileNodeByPath);
            if (resolvedTarget == null) continue;

            if (!localImportEdges.TryGetValue(fromNorm, out var targets))
            {
                targets = [];
                localImportEdges[fromNorm] = targets;
            }
            if (!targets.Contains(resolvedTarget, StringComparer.OrdinalIgnoreCase))
            {
                targets.Add(resolvedTarget);
            }
        }

        if (localImportEdges.Count == 0) return newRels;

        var existingContainsSet = new HashSet<(string ProjId, string FileId)>(
            existingProjectRels.Where(r => r.Kind == OntologyConstants.Relationships.Contains)
                               .Select(r => (r.From, r.To)));

        foreach (var (fromFile, directTargets) in localImportEdges)
        {
            if (!primaryOwnerByFilePath.TryGetValue(fromFile, out var consumerProj) || consumerProj.IsLibrary)
            {
                continue;
            }

            var queue = new Queue<string>();
            foreach (var dt in directTargets)
            {
                if (primaryOwnerByFilePath.TryGetValue(dt, out var targetOwner) &&
                    !string.Equals(targetOwner.Id, consumerProj.Id, StringComparison.OrdinalIgnoreCase) &&
                    !targetOwner.IsLibrary)
                {
                    queue.Enqueue(dt);
                }
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (queue.Count > 0)
            {
                var currentPath = queue.Dequeue();
                if (!visited.Add(currentPath)) continue;
                if (!fileNodeByPath.TryGetValue(currentPath, out var targetFileNode)) continue;

                if (existingContainsSet.Add((consumerProj.Id, targetFileNode.Id)))
                {
                    var rel = new Relationship(consumerProj.Id, targetFileNode.Id, OntologyConstants.Relationships.Contains);
                    newRels.Add(rel);
                    existingProjectRels.Add(rel);
                }

                if (localImportEdges.TryGetValue(currentPath, out var nestedTargets))
                {
                    foreach (var nt in nestedTargets)
                    {
                        if (!visited.Contains(nt) &&
                            primaryOwnerByFilePath.TryGetValue(nt, out var nestedOwner) &&
                            !nestedOwner.IsLibrary)
                        {
                            queue.Enqueue(nt);
                        }
                    }
                }
            }
        }

        return newRels;
    }

    private static string? TryResolveRelativeFileImport(
        string importerRelativePath,
        string rawImportSpec,
        Dictionary<string, FileNode> fileNodeByPath)
    {
        var lastSlash = importerRelativePath.LastIndexOf('/');
        var baseDir = lastSlash >= 0 ? importerRelativePath[..lastSlash] : "";
        var combined = string.IsNullOrEmpty(baseDir)
            ? rawImportSpec.Replace('\\', '/')
            : baseDir + "/" + rawImportSpec.Replace('\\', '/');

        var stack = new List<string>();
        foreach (var seg in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg == ".") continue;
            if (seg == "..")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(seg);
        }

        if (stack.Count == 0) return null;
        var normalizedCandidate = string.Join('/', stack);

        if (fileNodeByPath.ContainsKey(normalizedCandidate)) return normalizedCandidate;

        foreach (var ext in new[] { ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", "/index.ts", "/index.tsx", "/index.js", "/index.jsx" })
        {
            var withExt = normalizedCandidate + ext;
            if (fileNodeByPath.ContainsKey(withExt)) return withExt;
        }

        return null;
    }

    private static IEnumerable<(string Name, LambdaExprSyntax Lambda)> EnumerateNamedLambdaFunctions(SyntaxNode? root)
    {
        if (root == null) yield break;
        var queue = new Queue<SyntaxNode>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            if (curr is LambdaExprSyntax lambda)
            {
                yield return (InferLambdaName(lambda), lambda);
            }
            foreach (var child in curr.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    private static string InferLambdaName(LambdaExprSyntax lambda)
    {
        var nameNode = lambda.GetChildForField(SyntaxFields.Name) ??
                       lambda.NamedChildren.FirstOrDefault(c => c is IdentifierExprSyntax && c != lambda.Body);
        if (nameNode is IdentifierExprSyntax id && !string.IsNullOrWhiteSpace(id.Text))
        {
            return id.Text.Trim();
        }

        var curr = lambda.Parent;
        while (curr is BinaryExprSyntax or ConditionalExprSyntax)
        {
            curr = curr.Parent;
        }

        if (curr is VariableDeclSyntax varDecl)
        {
            var lhs = varDecl.GetChildForField(SyntaxFields.Name) ?? varDecl.GetChildForField(SyntaxFields.Pattern);
            var name = !string.IsNullOrEmpty(varDecl.Name) ? varDecl.Name : (lhs is IdentifierExprSyntax varId ? varId.Text.Trim() : null);
            if (!string.IsNullOrEmpty(name)) return name;
        }
        else if (curr is AssignmentExprSyntax assign)
        {
            var left = assign.Left ?? assign.GetChildForField(SyntaxFields.Left) ?? (assign.NamedChildren.Count > 0 ? assign.NamedChildren[0] : null);
            if (left != null)
            {
                var leftText = left.Text.Trim();
                if (leftText is "module.exports" or "exports")
                {
                    return "default";
                }
                if (left is MemberAccessExprSyntax mem)
                {
                    var prop = !string.IsNullOrEmpty(mem.MemberName)
                        ? mem.MemberName
                        : (mem.GetChildForField(SyntaxFields.Property) ?? (mem.NamedChildren.Count > 1 ? mem.NamedChildren[^1] : null))?.Text.Trim();
                    if (!string.IsNullOrEmpty(prop)) return prop;
                }
                else if (left is IdentifierExprSyntax leftId)
                {
                    var idName = !string.IsNullOrEmpty(leftId.Name) ? leftId.Name : leftId.Text.Trim();
                    if (!string.IsNullOrEmpty(idName)) return idName;
                }
            }
        }
        else if (curr?.RawGrammarKind == "export_statement")
        {
            return "default";
        }
        else if (curr?.RawGrammarKind == "pair")
        {
            var keyNode = curr.GetChildForField(SyntaxFields.Key) ?? (curr.NamedChildren.Count > 0 ? curr.NamedChildren[0] : null);
            if (keyNode != null)
            {
                var keyName = keyNode.Text.Trim().Trim('"', '\'', '`');
                if (!string.IsNullOrEmpty(keyName)) return keyName;
            }
        }

        return $"<anon_{lambda.Span.StartLine + 1}>";
    }
}
