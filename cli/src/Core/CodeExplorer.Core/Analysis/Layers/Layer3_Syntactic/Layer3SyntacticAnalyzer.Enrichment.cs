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
    private const string CallbackArgFlow = "callback_arg";

    private static bool HasTopLevelCallsOrStatements(SyntaxDocument doc)
    {
        if (doc.TopLevelStatements.Count > 0) return true;
        foreach (var call in doc.AllCalls)
        {
            if (IsTopLevelModuleInitCall(call)) return true;
        }
        return false;
    }

    private static bool IsTopLevelModuleInitCall(CallExprSyntax call)
    {
        var curr = call.Parent;
        while (curr != null)
        {
            if (curr is FunctionDeclSyntax or TypeDeclSyntax)
            {
                return false;
            }
            if (curr is LambdaExprSyntax lambda)
            {
                var calleeCandidate = (SyntaxNode)lambda;
                while (calleeCandidate.Parent is ParenthesizedExprSyntax)
                {
                    calleeCandidate = calleeCandidate.Parent;
                }
                if (calleeCandidate.Parent is CallExprSyntax iifeCall &&
                    (ReferenceEquals(iifeCall.Callee, calleeCandidate) ||
                     ReferenceEquals(iifeCall.Callee, lambda) ||
                     iifeCall.Span.StartLine == lambda.Span.StartLine))
                {
                    curr = iifeCall.Parent;
                    continue;
                }
                return false;
            }
            curr = curr.Parent;
        }
        return true;
    }

    internal static void PopulateModuleInitReferences(FunctionNode modInitNode, SyntaxDocument doc)
    {
        var seen = new HashSet<(string Target, int Line)>();
        foreach (var call in doc.AllCalls)
        {
            if (!IsTopLevelModuleInitCall(call)) continue;
            var targetName = ExtractModuleInitCallTarget(call);
            if (string.IsNullOrWhiteSpace(targetName) ||
                targetName is "require" or "__import__" or "import_module" or "define")
            {
                continue;
            }

            var line = call.Span.StartLine + 1;
            var col = call.Span.StartColumn + 1;
            if (seen.Add((targetName, line)))
            {
                modInitNode.References.Add(new Reference(
                    modInitNode.Id,
                    targetName,
                    OntologyConstants.Relationships.Calls,
                    line,
                    col));
            }

            foreach (var arg in call.Arguments)
            {
                if (arg.Value is IdentifierExprSyntax idArg)
                {
                    var argName = !string.IsNullOrEmpty(idArg.Name) ? idArg.Name : idArg.Text.Trim();
                    if (!string.IsNullOrEmpty(argName) &&
                        argName is not ("true" or "false" or "null" or "undefined" or "this" or "arguments" or "window" or "document" or "global" or "globalThis" or "process" or "console" or "Math" or "JSON" or "Object" or "Array" or "String" or "Number" or "Boolean" or "Promise" or "Error"))
                    {
                        var argLine = idArg.Span.StartLine + 1;
                        var argCol = idArg.Span.StartColumn + 1;
                        if (seen.Add((argName, argLine)))
                        {
                            modInitNode.References.Add(new Reference(
                                modInitNode.Id,
                                argName,
                                OntologyConstants.Relationships.Calls,
                                argLine,
                                argCol,
                                argFlow: CallbackArgFlow));
                        }
                    }
                }
            }
        }
    }

    private static string? ExtractModuleInitCallTarget(CallExprSyntax call)
    {
        if (call.Callee is MemberAccessExprSyntax mem)
        {
            var prop = !string.IsNullOrEmpty(mem.MemberName) ? mem.MemberName : call.MethodName;
            var obj = mem.Object;
            while (obj is ParenthesizedExprSyntax parenExpr)
            {
                obj = parenExpr.Expression ?? (parenExpr.Children.Count > 0 ? parenExpr.Children[0] as ExprSyntax : null);
            }
            if (obj is CallExprSyntax innerCall &&
                innerCall.MethodName == "require" &&
                innerCall.Arguments.Count > 0 &&
                innerCall.Arguments[0].Value is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String } reqLit &&
                !string.IsNullOrWhiteSpace(reqLit.ValueText) &&
                !string.IsNullOrWhiteSpace(prop))
            {
                return $"__req_{reqLit.ValueText}.{prop}";
            }

            if (obj is IdentifierExprSyntax objId)
            {
                var objName = !string.IsNullOrEmpty(objId.Name) ? objId.Name : objId.Text.Trim();
                if (!string.IsNullOrEmpty(objName) && !string.IsNullOrEmpty(prop))
                {
                    return $"{objName}.{prop}";
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(call.MethodName))
        {
            return call.MethodName;
        }

        if (call.Callee is IdentifierExprSyntax calleeId)
        {
            return !string.IsNullOrEmpty(calleeId.Name) ? calleeId.Name : calleeId.Text.Trim();
        }

        return null;
    }

    internal static string ExtractSyntaxFunctionName(FunctionDeclSyntax fnDecl)
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

    internal static FunctionDeclSyntax? FindEnclosingSyntaxFunction(SyntaxNode node)
    {
        var curr = node.Parent;
        while (curr != null)
        {
            if (curr is FunctionDeclSyntax fn) return fn;
            curr = curr.Parent;
        }
        return null;
    }

    internal static TypeDeclSyntax? FindEnclosingSyntaxType(SyntaxNode node)
    {
        var curr = node.Parent;
        while (curr != null)
        {
            if (curr is TypeDeclSyntax td) return td;
            curr = curr.Parent;
        }
        return null;
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

    private static bool IsSyntaxFunctionExported(FunctionDeclSyntax fnDecl, SyntaxDocument doc, bool hasLateExportMarkers = false)
    {
        if ((fnDecl.Modifiers & SyntaxModifiers.Export) != 0 || (fnDecl.Modifiers & SyntaxModifiers.Public) != 0)
        {
            return true;
        }

        var curr = fnDecl.Parent;
        while (curr != null)
        {
            if (curr is ImportDeclSyntax { IsReExport: true }) return true;
            if (curr is TypeDeclSyntax td)
            {
                if ((td.Modifiers & SyntaxModifiers.Export) != 0 || (td.Modifiers & SyntaxModifiers.Public) != 0) return true;
                if (td.Parent is ImportDeclSyntax { IsReExport: true }) return true;
                if (hasLateExportMarkers)
                {
                    var typeName = !string.IsNullOrWhiteSpace(td.Name)
                        ? td.Name
                        : (td.GetChildForField(SyntaxFields.Name) ?? td.NamedChildren.FirstOrDefault(c => c is IdentifierExprSyntax))?.Text.Trim();
                    if (!string.IsNullOrEmpty(typeName) && doc.SourceText.Contains(typeName, StringComparison.Ordinal) &&
                        (doc.SourceText.Contains("module.exports", StringComparison.Ordinal) || doc.SourceText.Contains("exports.", StringComparison.Ordinal)))
                    {
                        return true;
                    }
                }
            }
            curr = curr.Parent;
        }

        if (!hasLateExportMarkers) return false;

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

        fileNode.Flags |= SymbolFlags.HasParseIssues;

        const string stageLayer3Syntactic = "Layer3_Syntactic";
        const string issueNameSyntaxError = "SyntaxError";
        const string issueSeverityWarning = "warning";
        var lang = syntaxTree.Document?.LanguageName ?? syntaxTree.FileParser.LanguageName;
        for (var d = 0; d < diags.Count; d++)
        {
            var diag = diags[d];
            ctx.RecordScanIssue(
                stage: stageLayer3Syntactic,
                category: CodeExplorer.Cypher.Common.TaxonomyEnumExtendedExtensions.ToCanonicalString(diag.Kind),
                relativeOrFullPath: fileNode.Path,
                message: diag.Message,
                name: issueNameSyntaxError,
                severity: issueSeverityWarning,
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
        var memberCountsByName = new Dictionary<string, int>(StringComparer.Ordinal);
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
                const string classConfig = "config";
                const string classEtl = "etl";
                const string classConstant = "constant";
                const string classGlobal = "global";
                const string classProperty = "property";
                const string classField = "field";

                var varTypeStr = new List<string>();
                if (isConfig) varTypeStr.Add(classConfig);
                if (isEtl) varTypeStr.Add(classEtl);
                if (isConstant) varTypeStr.Add(classConstant);
                if (isGlobal) varTypeStr.Add(classGlobal);
                if (isProperty) varTypeStr.Add(classProperty);
                if (isField) varTypeStr.Add(classField);

                var varType = string.Join(",", varTypeStr);
                memberCountsByName.TryGetValue(rawVar.Name, out var count);
                count++;
                memberCountsByName[rawVar.Name] = count;
                var baseVarId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Member:{rawVar.Name}";
                var varId = count == 1 ? baseVarId : $"{baseVarId}:{rawVar.StartLine}";

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
            const string interfaceTypeKind = "interface";
            typedNode = new TypeNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1, interfaceTypeKind,
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

        if (ctx != null)
        {
            if (typedNode is TypeNode tn)
            {
                ctx.RegisterKnownTypeName(tn.Name);
            }
            else if (typedNode is FunctionNode fn)
            {
                ctx.RegisterKnownCallableName(fn.Name);
            }
        }

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
                typedNode.ApplySyntacticProperty(k, v);
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
        var nature = CodeExplorer.Core.Semantics.PlatformParameterCatalog.ResolveParameterNature(callee, argIdx);
        if (nature == CodeExplorer.Core.Semantics.ParameterNature.Unspecified)
        {
            nature = CodeExplorer.Core.Semantics.PlatformParameterCatalog.ClassifyByName(name);
        }

        return new CallLiteralNode(litId, name, val, callee, argIdx, relativePath, line, col, symbolIsTest, projectKind, projectName, role, isDecorator, nature);
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
            existingProjectRels.Where(r => r.Kind == CodeExplorer.Cypher.Common.RelationshipKind.Contains)
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
                continue;
            }
            if (curr is FunctionDeclSyntax or TypeDeclSyntax or UiElementSyntax) continue;
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
        else if (curr is ImportDeclSyntax { IsReExport: true })
        {
            return "default";
        }
        else if (curr is KeyValueExprSyntax kv)
        {
            var keyName = kv.KeyText.Trim().Trim('"', '\'', '`');
            if (!string.IsNullOrEmpty(keyName)) return keyName;
            var keyNode = kv.Key ?? kv.GetChildForField(SyntaxFields.Key) ?? (kv.NamedChildren.Count > 0 ? kv.NamedChildren[0] : null);
            if (keyNode != null)
            {
                keyName = keyNode.Text.Trim().Trim('"', '\'', '`');
                if (!string.IsNullOrEmpty(keyName)) return keyName;
            }
        }

        return $"<anon_{lambda.Span.StartLine + 1}>";
    }

    internal static List<ProjectNode> OrderProjectsTopologically(Layer2Result l2Result)
    {
        var projects = l2Result.Projects;
        if (projects.Count <= 1) return projects;

        var projectById = new Dictionary<string, ProjectNode>(projects.Count, StringComparer.OrdinalIgnoreCase);
        var indexById = new Dictionary<string, int>(projects.Count, StringComparer.OrdinalIgnoreCase);
        var dependentsById = new Dictionary<string, HashSet<string>>(projects.Count, StringComparer.OrdinalIgnoreCase);
        var inDegreeById = new Dictionary<string, int>(projects.Count, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < projects.Count; i++)
        {
            var p = projects[i];
            projectById[p.Id] = p;
            indexById[p.Id] = i;
            dependentsById[p.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            inDegreeById[p.Id] = 0;
        }

        for (var i = 0; i < l2Result.ProjectDependencies.Count; i++)
        {
            var rel = l2Result.ProjectDependencies[i];
            if (rel.Kind != CodeExplorer.Cypher.Common.RelationshipKind.DependsOn) continue;
            if (string.Equals(rel.From, rel.To, StringComparison.OrdinalIgnoreCase)) continue;
            if (!projectById.ContainsKey(rel.From) || !projectById.ContainsKey(rel.To)) continue;

            // rel.From depends on rel.To -> rel.To must be scanned before rel.From
            if (dependentsById[rel.To].Add(rel.From))
            {
                inDegreeById[rel.From]++;
            }
        }

        var queue = new PriorityQueue<string, (int Tier, int NegDependents, int OriginalIndex)>(projects.Count);
        for (var i = 0; i < projects.Count; i++)
        {
            var p = projects[i];
            if (inDegreeById[p.Id] == 0)
            {
                queue.Enqueue(p.Id, (GetProjectTopologicalTier(p), -dependentsById[p.Id].Count, i));
            }
        }

        var ordered = new List<ProjectNode>(projects.Count);
        var visited = new HashSet<string>(projects.Count, StringComparer.OrdinalIgnoreCase);

        while (queue.TryDequeue(out var readyId, out _))
        {
            if (!visited.Add(readyId)) continue;
            ordered.Add(projectById[readyId]);

            foreach (var consumerId in dependentsById[readyId])
            {
                var remaining = --inDegreeById[consumerId];
                if (remaining == 0 && !visited.Contains(consumerId))
                {
                    var consumer = projectById[consumerId];
                    queue.Enqueue(
                        consumerId,
                        (GetProjectTopologicalTier(consumer), -dependentsById[consumerId].Count, indexById[consumerId]));
                }
            }
        }

        if (ordered.Count < projects.Count)
        {
            var remaining = projects
                .Where(p => !visited.Contains(p.Id))
                .OrderBy(GetProjectTopologicalTier)
                .ThenByDescending(p => dependentsById[p.Id].Count)
                .ThenBy(p => indexById[p.Id]);
            ordered.AddRange(remaining);
        }

        return ordered;
    }

    private static int GetProjectTopologicalTier(ProjectNode project)
    {
        if (project.IsTest)
        {
            return 2;
        }
        if (project.IsLibrary ||
            project.EntityKind == ProjectEntityKind.Library ||
            HasSharedLibraryNameHint(project.Name) ||
            HasSharedLibraryNameHint(project.Path))
        {
            return 0;
        }
        return 1;
    }

    private static bool HasSharedLibraryNameHint(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.Contains("shared", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("common", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("core", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("utils", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("sdk", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("types", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("models", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("contracts", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("config", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("-ui", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("/ui", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class Layer3ScopeVisibilityPass
{
    internal static void RegisterFileRawImports(ParsingContext? ctx, string relativePath, List<RawImport> rawImports)
    {
        if (ctx == null || rawImports.Count == 0) return;
        var normRelPath = relativePath.Replace('\\', '/');
        for (int r = 0; r < rawImports.Count; r++)
        {
            var ri = rawImports[r];
            if (!string.IsNullOrWhiteSpace(ri.Path))
            {
                var nsSet = ctx.FileImportedNamespaces.GetOrAdd(normRelPath, _ => new HashSet<string>(StringComparer.Ordinal));
                lock (nsSet)
                {
                    nsSet.Add(ri.Path);
                }
            }
            if (ri.Specifiers != null)
            {
                for (int s = 0; s < ri.Specifiers.Count; s++)
                {
                    var spec = ri.Specifiers[s];
                    ctx.RegisterKnownCallableName(spec.LocalName);
                    ctx.RegisterKnownCallableName(spec.ImportedName);
                    ctx.RegisterKnownTypeName(spec.LocalName);
                    ctx.RegisterKnownTypeName(spec.ImportedName);
                }
            }
        }
    }

    internal static void RegisterSyntaxTreeWorkspaceSymbols(
        SyntaxTree syntaxTree,
        ParsingContext ctx)
    {
        var normRelPath = syntaxTree.RelativePath.Replace('\\', '/');
        var doc = syntaxTree.Document;
        if (doc != null)
        {
            if (!string.IsNullOrWhiteSpace(doc.PackageOrNamespace))
            {
                ctx.FileNamespaces[normRelPath] = doc.PackageOrNamespace;
            }

            for (var m = 0; m < doc.Imports.Count; m++)
            {
                var imp = doc.Imports[m];
                if (!string.IsNullOrWhiteSpace(imp.ModulePath))
                {
                    var nsSet = ctx.FileImportedNamespaces.GetOrAdd(normRelPath, _ => new HashSet<string>(StringComparer.Ordinal));
                    lock (nsSet)
                    {
                        nsSet.Add(imp.ModulePath);
                    }
                }
                if (!string.IsNullOrWhiteSpace(imp.Alias))
                {
                    ctx.RegisterKnownCallableName(imp.Alias);
                    ctx.RegisterKnownTypeName(imp.Alias);
                }
            }

            for (var f = 0; f < doc.AllFunctions.Count; f++)
            {
                var fnDecl = doc.AllFunctions[f];
                if (fnDecl.Kind == FunctionSyntaxKind.Accessor) continue;
                var fnName = Layer3SyntacticParser.ExtractSyntaxFunctionName(fnDecl);
                if (!fnName.StartsWith("<anon_", StringComparison.Ordinal))
                {
                    ctx.RegisterKnownCallableName(fnName);
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
                    ctx.RegisterKnownTypeName(tName);
                }
            }
        }

        if (syntaxTree.FileNode != null && syntaxTree.FileNode.Children.Count > 0)
        {
            RegisterOntologyTreeSymbolNames(syntaxTree.FileNode, ctx);
        }
    }

    private static void RegisterOntologyTreeSymbolNames(IOntologyNode parent, ParsingContext ctx)
    {
        for (var i = 0; i < parent.Children.Count; i++)
        {
            var child = parent.Children[i];
            if (child is TypeNode tn)
            {
                ctx.RegisterKnownTypeName(tn.Name);
            }
            else if (child is FunctionNode fn)
            {
                ctx.RegisterKnownCallableName(fn.Name);
            }
            if (child.Children.Count > 0)
            {
                RegisterOntologyTreeSymbolNames(child, ctx);
            }
        }
    }

    internal static void PopulateSymbolScopeMetadata(
        FileNode fileNode,
        SyntaxDocument doc,
        string relativePath,
        string? projectName,
        bool isTest,
        ParsingContext? ctx)
    {
        var hasLateExportMarkers = doc.SourceText.Contains("module.exports", StringComparison.Ordinal) ||
                                   doc.SourceText.Contains("exports.", StringComparison.Ordinal) ||
                                   doc.SourceText.Contains("export {", StringComparison.Ordinal) ||
                                   doc.SourceText.Contains("export default ", StringComparison.Ordinal);

        var normFilePath = relativePath.Replace('\\', '/');
        var fileNamespace = !string.IsNullOrWhiteSpace(doc.PackageOrNamespace) ? doc.PackageOrNamespace : null;
        if (ctx != null && fileNamespace != null)
        {
            ctx.FileNamespaces[normFilePath] = fileNamespace;
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
                    ctx?.RegisterKnownTypeName(tn.Name);
                    ctx?.SymbolScopes.TryAdd(tn.Id, new SymbolScopeInfo(
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
                    ctx?.RegisterKnownCallableName(fn.Name);
                    ctx?.SymbolScopes.TryAdd(fn.Id, new SymbolScopeInfo(
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
                ? (enclosingType.TypeKind == CodeExplorer.Cypher.Common.TypeKind.Interface ? SymbolVisibility.Public : SymbolVisibility.Private)
                : SymbolVisibility.Private,
            "go" => fn.Name.Length > 0 && char.IsAsciiLetterLower(fn.Name[0]) ? SymbolVisibility.Internal : SymbolVisibility.Public,
            "rust" => SymbolVisibility.Internal,
            "typescript" or "tsx" or "javascript" => enclosingType != null
                ? (fn.Name.StartsWith('#') ? SymbolVisibility.Private : SymbolVisibility.Public)
                : (hasModuleExports ? SymbolVisibility.FilePrivate : SymbolVisibility.Public),
            "python" => fn.Name.StartsWith("__", StringComparison.Ordinal) && !fn.Name.EndsWith("__", StringComparison.Ordinal)
                ? (enclosingType != null ? SymbolVisibility.Private : SymbolVisibility.FilePrivate)
                : SymbolVisibility.Public,
            "java" => enclosingType?.TypeKind == CodeExplorer.Cypher.Common.TypeKind.Interface ? SymbolVisibility.Public : SymbolVisibility.Internal,
            _ => SymbolVisibility.Public
        };
    }
}
