using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Parser.Layers;

internal static class Layer3LoopComplexityClassifier
{
    private const int MaxSmallConstantLoopBound = 64;
    private const char OpenBraceChar = '{';
    private const char OpenBracketChar = '[';
    private const char CloseParenChar = ')';
    private const char DotChar = '.';
    private const char LessThanChar = '<';
    private const char EqualChar = '=';
    private const char SpaceChar = ' ';
    private const char SemicolonChar = ';';
    private const string DotCountSuffix = ".Count";
    private const string DotLengthSuffix = ".Length";
    private const string KeywordIn = " in ";
    private const string KeywordOf = " of ";
    private const string KeywordRange = "range ";
    private const string TryGetValueMethod = "TryGetValue";
    private const string GetValueOrDefaultMethod = "GetValueOrDefault";
    private const string GetOrAddMethod = "GetOrAdd";
    private const string ToListMethod = "ToList";
    private const string ToArrayMethod = "ToArray";
    private const string ContainsMethod = "Contains";
    private const string IndexOfMethod = "IndexOf";
    private const string OutPrefix = "out ";
    private const string OutVarPrefix = "out var ";
    private const string ListPrefix = "List<";
    private const string IListPrefix = "IList<";
    private const string ReadOnlyListPrefix = "IReadOnlyList<";
    private const string EnumerablePrefix = "IEnumerable<";
    private const string ArraySuffix = "[]";
    private const string KeysSuffix = ".Keys";
    private const string ValuesSuffix = ".Values";

    private static readonly string[] BoundedSourceSuffixes =
    [
        "Suffixes",
        "Prefixes",
        "Extensions",
        "Attributes",
        "Tokens",
        "Keywords",
        "Markers",
        "Headers",
        "Chars",
        "Delimiters",
        "Patterns"
    ];

    private static readonly string[] UnpartitionedLookupSuffixes =
    [
        "Lookup",
        "ById",
        "ByName",
        "ByFile",
        "ByFileName",
        "ByType",
        "ByPath",
        "ByKey",
        "ByProject",
        "ByProjectId",
        "ByCanonical",
        "BySymbol",
        "ByShape",
        "ByScope",
        "Dictionary",
        "Registry"
    ];

    private static readonly string[] UnpartitionedGlobalTokens =
    [
        "AllFiles",
        "AllProjects",
        "AllTrees",
        "AllRelationships",
        "AllResources",
        "GlobalReferences",
        "GlobalSymbols",
        "GlobalBindingsLookup",
        "GlobalProjectDependencies",
        "KnownFiles",
        "FileScopedSymbols",
        "OverloadedFileScopedSymbols",
        "ProjectsInDepGraph",
        "WorkspaceFiles"
    ];

    private static readonly string[] SmallArityReceiverSuffixes =
    [
        ".Arguments",
        ".Parameters",
        ".Attributes",
        ".Labels",
        ".Modifiers",
        ".TypeArguments"
    ];

    private static readonly HashSet<string> PredicateLinearScanMethods = new(StringComparer.Ordinal)
    {
        "FirstOrDefault",
        "First",
        "SingleOrDefault",
        "Single",
        "LastOrDefault",
        "Last",
        "Find",
        "FindAll",
        "FindIndex",
        "FindLast",
        "RemoveAll",
        "Where",
        "Any",
        "All",
        "Count",
        "find",
        "findIndex",
        "filter",
        "some",
        "every"
    };
    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> PredicateLinearScanLookup =
        PredicateLinearScanMethods.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly HashSet<string> FunctionalLinearScanSubset = new(StringComparer.Ordinal)
    {
        "Where",
        "Any",
        "All",
        "filter",
        "some",
        "every"
    };
    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> FunctionalLinearScanLookup =
        FunctionalLinearScanSubset.GetAlternateLookup<ReadOnlySpan<char>>();

    public static void CollectLocalCollectionHints(
        SyntaxNode bodyNode,
        out HashSet<string>? localListVars,
        out HashSet<string>? partitionedVars)
    {
        localListVars = null;
        partitionedVars = null;
        WalkForCollectionHints(bodyNode, ref localListVars, ref partitionedVars);
    }

    public static bool IsFunctionalLinearScan(ReadOnlySpan<char> methodNameSpan) =>
        FunctionalLinearScanLookup.Contains(methodNameSpan);

    public static bool IsDisguisedLinearScanCall(CallExprSyntax call, HashSet<string>? localListVars)
    {
        if (call.CallKind != CallSyntaxKind.Method || call.Arguments.Count == 0)
        {
            return false;
        }

        var methodSpan = call.MethodNameSpan;
        if (methodSpan.IsEmpty)
        {
            return false;
        }

        // Skip calls that already materialize a Lambda loop ScopeNode via FunctionalIterationMethods
        if (FunctionalLinearScanLookup.Contains(methodSpan) && HasLambdaArgument(call))
        {
            return false;
        }

        if (PredicateLinearScanLookup.Contains(methodSpan))
        {
            if (call.Receiver != null && EndsWithAnyToken(call.Receiver.AsSpan().Trim(), SmallArityReceiverSuffixes))
            {
                return false;
            }

            return true;
        }

        if (localListVars is { Count: > 0 } &&
            (methodSpan.SequenceEqual(ContainsMethod.AsSpan()) || methodSpan.SequenceEqual(IndexOfMethod.AsSpan())) &&
            call.Receiver is IdentifierExprSyntax recvId &&
            !recvId.NameSpan.IsEmpty)
        {
            var alt = localListVars.GetAlternateLookup<ReadOnlySpan<char>>();
            return alt.Contains(recvId.NameSpan);
        }

        return false;
    }

    public static LoopSourceKind ClassifyLoopSource(
        ControlFlowStmtSyntax loopStmt,
        HashSet<string>? partitionedVars)
    {
        var header = GetExtendedLoopHeaderSpan(loopStmt);
        if (header.IsEmpty)
        {
            return LoopSourceKind.Unspecified;
        }

        if (HasSmallNumericUpperBound(header))
        {
            return LoopSourceKind.Bounded;
        }

        var sourceExpr = ExtractIteratedExpressionSpan(header);
        if (sourceExpr.IsEmpty)
        {
            return LoopSourceKind.Collection;
        }

        if (EndsWithAnyToken(sourceExpr, BoundedSourceSuffixes))
        {
            return LoopSourceKind.Bounded;
        }

        if (sourceExpr.IndexOf(OpenBracketChar) >= 0 ||
            sourceExpr.Contains(GetValueOrDefaultMethod.AsSpan(), StringComparison.Ordinal) ||
            sourceExpr.Contains(TryGetValueMethod.AsSpan(), StringComparison.Ordinal))
        {
            return LoopSourceKind.Partitioned;
        }

        if (partitionedVars is { Count: > 0 })
        {
            var trimmedIdent = TrimTrailingCountOrLength(sourceExpr);
            var alt = partitionedVars.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!trimmedIdent.IsEmpty && alt.Contains(trimmedIdent))
            {
                return LoopSourceKind.Partitioned;
            }
        }

        if (sourceExpr.EndsWith(KeysSuffix.AsSpan(), StringComparison.Ordinal) ||
            sourceExpr.EndsWith(ValuesSuffix.AsSpan(), StringComparison.Ordinal) ||
            EndsWithAnyToken(sourceExpr, UnpartitionedLookupSuffixes) ||
            ContainsAnyToken(sourceExpr, UnpartitionedGlobalTokens))
        {
            return LoopSourceKind.Unpartitioned;
        }

        return LoopSourceKind.Collection;
    }

    private static void WalkForCollectionHints(
        SyntaxNode node,
        ref HashSet<string>? localListVars,
        ref HashSet<string>? partitionedVars)
    {
        for (var i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            if (child is FunctionDeclSyntax or TypeDeclSyntax)
            {
                continue;
            }

            if (child is VariableDeclSyntax vd && !vd.NameSpan.IsEmpty)
            {
                if (IsListTypedOrInitialized(vd))
                {
                    localListVars ??= new HashSet<string>(StringComparer.Ordinal);
                    localListVars.Add(vd.Name);
                }
                else if (IsPartitionedInitializer(vd.Initializer))
                {
                    partitionedVars ??= new HashSet<string>(StringComparer.Ordinal);
                    partitionedVars.Add(vd.Name);
                }
            }
            else if (child is CallExprSyntax call &&
                     call.Arguments.Count >= 2 &&
                     call.MethodNameSpan.SequenceEqual(TryGetValueMethod.AsSpan()))
            {
                var outVar = ExtractOutVariableName(call.Arguments[1].AsSpan());
                if (!outVar.IsEmpty)
                {
                    partitionedVars ??= new HashSet<string>(StringComparer.Ordinal);
                    partitionedVars.Add(outVar.ToString());
                }
            }

            if (child.Children.Count > 0)
            {
                WalkForCollectionHints(child, ref localListVars, ref partitionedVars);
            }
        }
    }

    private static bool IsListTypedOrInitialized(VariableDeclSyntax vd)
    {
        if (vd.VariableType != null && !vd.VariableType.NameSpan.IsEmpty)
        {
            var tSpan = vd.VariableType.AsSpan().Trim();
            if (tSpan.StartsWith(ListPrefix.AsSpan(), StringComparison.Ordinal) ||
                tSpan.StartsWith(IListPrefix.AsSpan(), StringComparison.Ordinal) ||
                tSpan.StartsWith(ReadOnlyListPrefix.AsSpan(), StringComparison.Ordinal) ||
                tSpan.StartsWith(EnumerablePrefix.AsSpan(), StringComparison.Ordinal) ||
                tSpan.EndsWith(ArraySuffix.AsSpan(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (vd.Initializer is CollectionExprSyntax { Kind: CollectionSyntaxKind.Array })
        {
            return true;
        }

        if (vd.Initializer is CallExprSyntax initCall)
        {
            var mSpan = initCall.MethodNameSpan;
            if (mSpan.SequenceEqual(ToListMethod.AsSpan()) ||
                mSpan.SequenceEqual(ToArrayMethod.AsSpan()) ||
                (initCall.CallKind == CallSyntaxKind.Constructor &&
                 initCall.AsSpan().Contains(ListPrefix.AsSpan(), StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPartitionedInitializer(ExprSyntax? init)
    {
        if (init is IndexExprSyntax)
        {
            return true;
        }

        if (init is CallExprSyntax call)
        {
            var mSpan = call.MethodNameSpan;
            if (mSpan.SequenceEqual(GetValueOrDefaultMethod.AsSpan()) ||
                mSpan.SequenceEqual(GetOrAddMethod.AsSpan()))
            {
                return true;
            }
        }

        if (init is BinaryExprSyntax { Operator: "??" } bin)
        {
            return IsPartitionedInitializer(bin.Left);
        }

        return false;
    }

    private static ReadOnlySpan<char> ExtractOutVariableName(ReadOnlySpan<char> argSpan)
    {
        var trimmed = argSpan.Trim();
        if (trimmed.StartsWith(OutVarPrefix.AsSpan(), StringComparison.Ordinal))
        {
            return trimmed[OutVarPrefix.Length..].Trim();
        }
        if (trimmed.StartsWith(OutPrefix.AsSpan(), StringComparison.Ordinal))
        {
            var rest = trimmed[OutPrefix.Length..].Trim();
            var spaceIdx = rest.IndexOf(SpaceChar);
            return spaceIdx > 0 ? rest[(spaceIdx + 1)..].Trim() : rest;
        }
        return ReadOnlySpan<char>.Empty;
    }

    private static bool HasLambdaArgument(CallExprSyntax call)
    {
        for (var i = 0; i < call.Arguments.Count; i++)
        {
            var arg = call.Arguments[i];
            if (arg.Value is LambdaExprSyntax || arg.FirstChildOfType<LambdaExprSyntax>() != null)
            {
                return true;
            }
        }
        return false;
    }

    private static ReadOnlySpan<char> GetExtendedLoopHeaderSpan(SyntaxNode node)
    {
        var span = node.AsSpan().TrimStart();
        var braceIdx = span.IndexOf(OpenBraceChar);
        if (braceIdx > 0)
        {
            span = span[..braceIdx];
        }
        if (span.Length > 160)
        {
            span = span[..160];
        }
        return span.Trim();
    }

    private static bool HasSmallNumericUpperBound(ReadOnlySpan<char> header)
    {
        var ltIdx = header.IndexOf(LessThanChar);
        if (ltIdx < 0 || ltIdx + 1 >= header.Length)
        {
            return false;
        }

        var rhs = header[(ltIdx + 1)..];
        if (!rhs.IsEmpty && rhs[0] == EqualChar)
        {
            rhs = rhs[1..];
        }
        rhs = rhs.TrimStart();
        var len = 0;
        while (len < rhs.Length && char.IsAsciiDigit(rhs[len]))
        {
            len++;
        }

        if (len == 0 || len > 3)
        {
            return false;
        }

        if (len < rhs.Length && (char.IsLetterOrDigit(rhs[len]) || rhs[len] == DotChar))
        {
            return false;
        }

        return int.TryParse(rhs[..len], out var bound) && bound <= MaxSmallConstantLoopBound;
    }

    private static ReadOnlySpan<char> ExtractIteratedExpressionSpan(ReadOnlySpan<char> header)
    {
        var inIdx = header.IndexOf(KeywordIn.AsSpan(), StringComparison.Ordinal);
        if (inIdx >= 0)
        {
            return TrimTrailingCloseParen(header[(inIdx + KeywordIn.Length)..]);
        }

        var ofIdx = header.IndexOf(KeywordOf.AsSpan(), StringComparison.Ordinal);
        if (ofIdx >= 0)
        {
            return TrimTrailingCloseParen(header[(ofIdx + KeywordOf.Length)..]);
        }

        var rangeIdx = header.IndexOf(KeywordRange.AsSpan(), StringComparison.Ordinal);
        if (rangeIdx >= 0)
        {
            return TrimTrailingCloseParen(header[(rangeIdx + KeywordRange.Length)..]);
        }

        var ltIdx = header.IndexOf(LessThanChar);
        if (ltIdx >= 0 && ltIdx + 1 < header.Length)
        {
            var rhs = header[(ltIdx + 1)..];
            if (!rhs.IsEmpty && rhs[0] == EqualChar) rhs = rhs[1..];
            var semiIdx = rhs.IndexOf(SemicolonChar);
            if (semiIdx > 0) rhs = rhs[..semiIdx];
            return TrimTrailingCountOrLength(rhs.Trim());
        }

        return ReadOnlySpan<char>.Empty;
    }

    private static ReadOnlySpan<char> TrimTrailingCloseParen(ReadOnlySpan<char> span)
    {
        var trimmed = span.Trim();
        if (!trimmed.IsEmpty && trimmed[^1] == CloseParenChar)
        {
            trimmed = trimmed[..^1].TrimEnd();
        }
        return trimmed;
    }

    private static ReadOnlySpan<char> TrimTrailingCountOrLength(ReadOnlySpan<char> span)
    {
        var s = span.Trim();
        if (s.EndsWith(DotCountSuffix.AsSpan(), StringComparison.Ordinal)) return s[..^DotCountSuffix.Length];
        if (s.EndsWith(DotLengthSuffix.AsSpan(), StringComparison.Ordinal)) return s[..^DotLengthSuffix.Length];
        return s;
    }

    private static bool EndsWithAnyToken(ReadOnlySpan<char> sourceExpr, string[] suffixes)
    {
        var clean = TrimTrailingCountOrLength(sourceExpr);
        for (var i = 0; i < suffixes.Length; i++)
        {
            if (clean.EndsWith(suffixes[i].AsSpan(), StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsAnyToken(ReadOnlySpan<char> sourceExpr, string[] tokens)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            var tok = tokens[i].AsSpan();
            var idx = sourceExpr.IndexOf(tok, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var beforeOk = idx == 0 || !char.IsLetterOrDigit(sourceExpr[idx - 1]);
                var afterPos = idx + tok.Length;
                var afterOk = afterPos >= sourceExpr.Length || !char.IsLetterOrDigit(sourceExpr[afterPos]);
                if (beforeOk && afterOk)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
