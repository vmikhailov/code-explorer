using System.Text;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Semantics;

internal readonly record struct SemanticFieldInitInfo(string? ResolvedValue, int Line);

internal sealed class SemanticFunctionScopeContext
{
    public required FunctionDeclSyntax Syntax { get; init; }
    public required CodeExplorer.Core.Common.Nodes.Layer3_Syntactic.FunctionNode Node { get; init; }
    public required Dictionary<string, int> ParamIndexByName { get; init; }
    public required List<SemanticParamSignal> Params { get; init; }
    public string? EnclosingTypeName { get; init; }
}

/// <summary>
/// Expression, template, configuration, and reaching-definition evaluators used by <see cref="Layer3SemanticArgumentCollector"/>.
/// </summary>
internal static class Layer3ArgumentExpressionEvaluator
{
    private const int MaxResolutionDepth = 6;
    private const string PlusOperator = "+";
    private const string ParamPlaceholderPrefix = "{param:";
    private const string PlaceholderEnd = "}";
    private const string ExprPlaceholder = "{expr}";

    private static readonly HashSet<string> ConfigCallNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "GetEnvironmentVariable", "getenv", "LookupEnv", "var",
        "GetConnectionString", "GetValue", "GetSection"
    };

    private static readonly string[] EnvPrefixes =
    [
        "process.env.", "import.meta.env.", "os.environ", "System.getenv", "ENV["
    ];

    private static readonly string[] ConfigReceiverNames =
    [
        "Configuration", "_configuration", "configuration", "_config", "config", "configService", "_configService", "env", "os.environ"
    ];

    public static ExprSyntax UnwrapTransparentWrappers(ExprSyntax expr)
    {
        while (true)
        {
            if (expr is ParenthesizedExprSyntax { IsInterpolation: false, Expression: { } innerParen })
            {
                expr = innerParen;
                continue;
            }
            if (expr is TypeCastExprSyntax { Operand: { } innerCast })
            {
                expr = innerCast;
                continue;
            }
            if (expr is UnaryExprSyntax { IsAwait: true, Operand: { } innerAwait })
            {
                expr = innerAwait;
                continue;
            }
            return expr;
        }
    }

    public static bool IsPotentialSemanticCandidate(ExprSyntax expr)
    {
        expr = UnwrapTransparentWrappers(expr);
        return expr switch
        {
            LiteralExprSyntax lit => lit.LiteralKind is LiteralSyntaxKind.String or LiteralSyntaxKind.RawString,
            LambdaExprSyntax => false,
            CollectionExprSyntax => false,
            _ => true
        };
    }

    public static bool ContainsDynamicOrParamReference(SyntaxNode node, SemanticFunctionScopeContext? fnScope, int depth = 0)
    {
        if (depth > MaxResolutionDepth)
        {
            return false;
        }
        if (node is ExprSyntax exprNode && TryDetectConfigOrEnvOrigin(exprNode, projectName: null, out _))
        {
            return true;
        }
        if (node is IdentifierExprSyntax id)
        {
            if (fnScope != null && fnScope.ParamIndexByName.ContainsKey(id.Name))
            {
                return true;
            }
            var localInit = FindLocalVariableInitializer(id, out _);
            if (localInit != null && ContainsDynamicOrParamReference(localInit, fnScope, depth + 1))
            {
                return true;
            }
        }
        if (node is CallExprSyntax || (node is MemberAccessExprSyntax ma && ma.IsEnvironmentLookup))
        {
            return true;
        }
        for (var i = 0; i < node.Children.Count; i++)
        {
            if (ContainsDynamicOrParamReference(node.Children[i], fnScope, depth + 1))
                return true;
        }
        return false;
    }

    public static bool ReferencesClassField(
        SyntaxNode node,
        SemanticFunctionScopeContext? fnScope,
        Dictionary<(string TypeName, string FieldName), SemanticFieldInitInfo> fieldInits,
        out string? fieldSymbol,
        out int? fieldLine)
    {
        fieldSymbol = null;
        fieldLine = null;

        if (node is IdentifierExprSyntax id && TryLookupFieldInit(id.Name, fnScope?.EnclosingTypeName, fieldInits, out var info))
        {
            fieldSymbol = id.Name;
            fieldLine = info.Line;
            return true;
        }

        if (node is MemberAccessExprSyntax ma && ma.StartsWithSelfOrSuper() &&
            TryLookupFieldInit(ma.MemberName, fnScope?.EnclosingTypeName, fieldInits, out var selfInfo))
        {
            fieldSymbol = ma.MemberName;
            fieldLine = selfInfo.Line;
            return true;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (ReferencesClassField(node.Children[i], fnScope, fieldInits, out fieldSymbol, out fieldLine))
                return true;
        }
        return false;
    }

    public static string? TryBuildBinaryCompositeTemplate(
        BinaryExprSyntax bin,
        SemanticFunctionScopeContext? fnScope,
        Dictionary<(string TypeName, string FieldName), SemanticFieldInitInfo> fieldInits,
        string? projectName)
    {
        var sb = new StringBuilder();
        var sawParam = false;

        bool AppendPart(ExprSyntax? part)
        {
            if (part == null) return false;
            part = UnwrapTransparentWrappers(part);

            if (part is BinaryExprSyntax { Operator: PlusOperator } subBin)
            {
                return AppendPart(subBin.Left) && AppendPart(subBin.Right);
            }

            if (part is IdentifierExprSyntax id && fnScope != null && fnScope.ParamIndexByName.TryGetValue(id.Name, out var pIdx))
            {
                sb.Append(ParamPlaceholderPrefix).Append(pIdx).Append(PlaceholderEnd);
                sawParam = true;
                return true;
            }

            if (part is IdentifierExprSyntax fldId && TryLookupFieldInit(fldId.Name, fnScope?.EnclosingTypeName, fieldInits, out var fInfo) && fInfo.ResolvedValue != null)
            {
                sb.Append(fInfo.ResolvedValue);
                return true;
            }

            if (part is MemberAccessExprSyntax ma && ma.StartsWithSelfOrSuper() &&
                TryLookupFieldInit(ma.MemberName, fnScope?.EnclosingTypeName, fieldInits, out var sfInfo) && sfInfo.ResolvedValue != null)
            {
                sb.Append(sfInfo.ResolvedValue);
                return true;
            }

            if (AstValueResolver.TryResolveString(part, projectName, out var strVal))
            {
                sb.Append(strVal);
                return true;
            }

            return false;
        }

        return AppendPart(bin) && sawParam ? sb.ToString() : null;
    }

    public static bool HasInterpolationHoles(SyntaxNode expr)
    {
        for (var i = 0; i < expr.Children.Count; i++)
        {
            if (expr.Children[i] is ParenthesizedExprSyntax { IsInterpolation: true })
                return true;
        }
        return false;
    }

    public static void AnalyzeInterpolatedExpression(
        ExprSyntax expr,
        SemanticFunctionScopeContext? fnScope,
        Dictionary<(string TypeName, string FieldName), SemanticFieldInitInfo> fieldInits,
        string? projectName,
        out string? resolvedValue,
        out string? compositeTemplate,
        out ArgumentProvenance provenance,
        out int? referencedParamIdx,
        out string? originSymbol,
        out int? originLine)
    {
        resolvedValue = null;
        compositeTemplate = null;
        provenance = ArgumentProvenance.DynamicComposite;
        referencedParamIdx = null;
        originSymbol = null;
        originLine = null;

        if (expr.Document == null) return;

        var doc = expr.Document;
        var span = expr.Span;
        var rawSpan = doc.AsSpan(new SourceSlice(span.StartByte, span.EndByte - span.StartByte));
        if (rawSpan.Length < 2) return;

        var startOffset = 0;
        while (startOffset < rawSpan.Length && rawSpan[startOffset] is '$' or '@' or 'f' or 'F' or 'r' or 'R')
        {
            startOffset++;
        }
        if (startOffset < rawSpan.Length && rawSpan[startOffset] is '"' or '\'' or '`')
        {
            startOffset++;
        }

        var endOffset = rawSpan.Length;
        if (endOffset > startOffset && rawSpan[endOffset - 1] is '"' or '\'' or '`')
        {
            endOffset--;
        }

        var contentStartByte = span.StartByte + startOffset;
        var contentEndByte = span.StartByte + endOffset;
        if (contentEndByte <= contentStartByte) return;

        var exactSb = new StringBuilder();
        var templateSb = new StringBuilder();
        var allHolesResolved = true;
        var cursor = contentStartByte;

        for (var i = 0; i < expr.Children.Count; i++)
        {
            var child = expr.Children[i];
            if (child is not ParenthesizedExprSyntax { IsInterpolation: true } hole)
            {
                continue;
            }

            var holeStart = Math.Max(contentStartByte, hole.Span.StartByte);
            var holeEnd = Math.Min(contentEndByte, hole.Span.EndByte);
            if (holeStart > cursor)
            {
                var prefix = doc.GetText(new SourceSlice(cursor, holeStart - cursor));
                exactSb.Append(prefix);
                templateSb.Append(prefix);
            }

            var inner = hole.Expression;
            if (inner != null)
            {
                inner = UnwrapTransparentWrappers(inner);
                if (inner is IdentifierExprSyntax id && fnScope != null && fnScope.ParamIndexByName.TryGetValue(id.Name, out var pIdx))
                {
                    allHolesResolved = false;
                    referencedParamIdx ??= pIdx;
                    provenance = ArgumentProvenance.CallerParameter;
                    originSymbol ??= id.Name;
                    originLine ??= fnScope.Params[pIdx].Line;
                    templateSb.Append(ParamPlaceholderPrefix).Append(pIdx).Append(PlaceholderEnd);
                }
                else if (inner is IdentifierExprSyntax fId && TryLookupFieldInit(fId.Name, fnScope?.EnclosingTypeName, fieldInits, out var fInit) && fInit.ResolvedValue != null)
                {
                    provenance = ArgumentProvenance.MemberOrField;
                    originSymbol ??= fId.Name;
                    originLine ??= fInit.Line;
                    exactSb.Append(fInit.ResolvedValue);
                    templateSb.Append(fInit.ResolvedValue);
                }
                else if (inner is MemberAccessExprSyntax ma && ma.StartsWithSelfOrSuper() &&
                         TryLookupFieldInit(ma.MemberName, fnScope?.EnclosingTypeName, fieldInits, out var sfInit) && sfInit.ResolvedValue != null)
                {
                    provenance = ArgumentProvenance.MemberOrField;
                    originSymbol ??= ma.MemberName;
                    originLine ??= sfInit.Line;
                    exactSb.Append(sfInit.ResolvedValue);
                    templateSb.Append(sfInit.ResolvedValue);
                }
                else if (AstValueResolver.TryResolveString(inner, projectName, out var holeConst))
                {
                    exactSb.Append(holeConst);
                    templateSb.Append(holeConst);
                }
                else
                {
                    allHolesResolved = false;
                    if (TryDetectConfigOrEnvOrigin(inner, projectName, out var cfgKey) && provenance == ArgumentProvenance.DynamicComposite)
                    {
                        provenance = ArgumentProvenance.ConfigOrEnv;
                        originSymbol ??= cfgKey;
                    }
                    templateSb.Append(ExprPlaceholder);
                }
            }

            cursor = holeEnd;
        }

        if (cursor < contentEndByte)
        {
            var tail = doc.GetText(new SourceSlice(cursor, contentEndByte - cursor));
            exactSb.Append(tail);
            templateSb.Append(tail);
        }

        if (allHolesResolved)
        {
            resolvedValue = exactSb.ToString();
            if (provenance == ArgumentProvenance.DynamicComposite)
            {
                provenance = ArgumentProvenance.InternalConstant;
            }
        }
        else
        {
            compositeTemplate = templateSb.ToString();
        }
    }

    public static bool TryDetectConfigOrEnvOrigin(ExprSyntax expr, string? projectName, out string? configKey)
    {
        configKey = null;

        if (expr is MemberAccessExprSyntax ma)
        {
            if (ma.IsEnvironmentLookup)
            {
                configKey = ma.MemberName;
                return true;
            }

            var rawText = ma.Text.Trim();
            for (var i = 0; i < EnvPrefixes.Length; i++)
            {
                if (rawText.StartsWith(EnvPrefixes[i], StringComparison.Ordinal))
                {
                    configKey = ma.MemberName;
                    return true;
                }
            }
        }

        if (expr is CallExprSyntax call && ConfigCallNames.Contains(call.MethodName))
        {
            if (call.Arguments.Count > 0 && call.Arguments[0].Value != null &&
                AstValueResolver.TryResolveString(call.Arguments[0].Value, projectName, out var keyStr))
            {
                configKey = keyStr;
            }
            else
            {
                configKey = call.MethodName;
            }
            return true;
        }

        if (expr is IndexExprSyntax idx && idx.Target != null)
        {
            var targetText = idx.Target.Text.Trim();
            var shortTarget = PlatformParameterCatalog.ExtractShortMethodName(targetText);
            for (var i = 0; i < ConfigReceiverNames.Length; i++)
            {
                if (targetText.Equals(ConfigReceiverNames[i], StringComparison.OrdinalIgnoreCase) ||
                    shortTarget.Equals(ConfigReceiverNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    if (idx.Index != null && AstValueResolver.TryResolveString(idx.Index, projectName, out var idxKey))
                    {
                        configKey = idxKey;
                    }
                    else
                    {
                        configKey = shortTarget;
                    }
                    return true;
                }
            }
        }

        return false;
    }

    public static bool TryLookupFieldInit(
        string fieldName,
        string? enclosingTypeName,
        Dictionary<(string TypeName, string FieldName), SemanticFieldInitInfo> fieldInits,
        out SemanticFieldInitInfo info)
    {
        if (!string.IsNullOrEmpty(enclosingTypeName) &&
            fieldInits.TryGetValue((enclosingTypeName, fieldName), out info))
        {
            return true;
        }

        return fieldInits.TryGetValue((string.Empty, fieldName), out info);
    }

    public static ExprSyntax? FindLocalVariableInitializer(IdentifierExprSyntax ident, out int defLine)
    {
        defLine = 0;
        var targetName = ident.Name;
        if (string.IsNullOrEmpty(targetName)) return null;

        ExprSyntax? latestMatch = null;
        var current = ident.Parent;

        while (current != null)
        {
            for (var i = 0; i < current.Children.Count; i++)
            {
                var sibling = current.Children[i];
                if (sibling.Span.StartByte >= ident.Span.StartByte)
                {
                    break;
                }

                if (TryExtractLocalDefinitionInStmt(sibling, targetName, ident.Span.StartByte, out var initExpr, out var line))
                {
                    latestMatch = initExpr;
                    defLine = line;
                }
            }

            if (latestMatch != null || current is FunctionDeclSyntax)
            {
                break;
            }

            current = current.Parent;
        }

        return latestMatch;
    }

    private static bool TryExtractLocalDefinitionInStmt(
        SyntaxNode node,
        string targetName,
        int beforeByte,
        out ExprSyntax? initExpr,
        out int line)
    {
        initExpr = null;
        line = 0;

        if (node.Span.StartByte >= beforeByte || node.Span.EndByte > beforeByte || node is FunctionDeclSyntax or LambdaExprSyntax)
        {
            return false;
        }

        if (node is VariableDeclSyntax vd)
        {
            if (vd.Name.Equals(targetName, StringComparison.Ordinal) && vd.Initializer != null)
            {
                initExpr = vd.Initializer;
                line = vd.Span.StartLine + 1;
                return true;
            }

            for (var i = 0; i < vd.Children.Count; i++)
            {
                if (vd.Children[i] is VariableDeclSyntax subVd &&
                    subVd.Name.Equals(targetName, StringComparison.Ordinal) &&
                    subVd.Initializer != null)
                {
                    initExpr = subVd.Initializer;
                    line = subVd.Span.StartLine + 1;
                    return true;
                }
            }
        }

        if (node is AssignmentExprSyntax assign &&
            assign.Left is IdentifierExprSyntax leftId &&
            leftId.Name.Equals(targetName, StringComparison.Ordinal) &&
            assign.Right != null)
        {
            initExpr = assign.Right;
            line = assign.Span.StartLine + 1;
            return true;
        }

        if (node is ControlFlowStmtSyntax { Kind: ControlFlowKind.Expression })
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (TryExtractLocalDefinitionInStmt(node.Children[i], targetName, beforeByte, out initExpr, out line))
                    return true;
            }
        }

        return false;
    }

    public static FunctionDeclSyntax? FindEnclosingFunctionDecl(SyntaxNode node)
    {
        var curr = node.Parent;
        while (curr != null)
        {
            if (curr is FunctionDeclSyntax fn) return fn;
            curr = curr.Parent;
        }
        return null;
    }

    public static TypeDeclSyntax? FindEnclosingTypeDecl(SyntaxNode node)
    {
        var curr = node.Parent;
        while (curr != null)
        {
            if (curr is TypeDeclSyntax td) return td;
            curr = curr.Parent;
        }
        return null;
    }
}
