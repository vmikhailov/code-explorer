using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Analysis.Dataflow;

public enum FlowSourceKind : byte
{
    Parameter,      // Function parameter p0..pN
    InstanceField,  // this.fieldName / self.fieldName
    CallReturn,     // Return value of an invoked method
    ExternalSource, // ENV, Config, HTTP Request, DB Read
    Constant        // Literal or resolved constant
}

public readonly record struct DataflowOrigin(
    FlowSourceKind Kind,
    string Identifier,          // e.g. "p0" or "this._repo" or "call:GetUser"
    string? PropertyPath = null // e.g. ".UserId" when accessed via p0.UserId
)
{
    public override string ToString() =>
        string.IsNullOrEmpty(PropertyPath) ? Identifier : $"{Identifier}{PropertyPath}";
}

public sealed class FunctionDataflowSummary
{
    /// <summary>Origins that contribute to the function's return value.</summary>
    public List<DataflowOrigin> ReturnOrigins { get; } = [];

    /// <summary>Per-call-site mapping: (CalleeMethodName, Line) -> (ArgIndex -> Origins).</summary>
    public Dictionary<(string Callee, int Line), Dictionary<int, List<DataflowOrigin>>> CallArgFlows { get; } = new();

    /// <summary>Instance fields mutated by this function and their value origins.</summary>
    public Dictionary<string, List<DataflowOrigin>> FieldWrites { get; } = new(StringComparer.Ordinal);

    /// <summary>Compact string encoding stored on FunctionNode.Extensions["df_summary"].</summary>
    public string ToCompactString()
    {
        var parts = new List<string>();
        if (ReturnOrigins.Count > 0)
        {
            var ret = string.Join(",", ReturnOrigins.Select(o => o.ToString()).Distinct());
            parts.Add($"ret:{ret}");
        }
        if (FieldWrites.Count > 0)
        {
            var writes = string.Join(";", FieldWrites.Select(kv => $"{kv.Key}<-{string.Join(",", kv.Value.Select(o => o.ToString()).Distinct())}"));
            parts.Add($"writes:{writes}");
        }
        if (CallArgFlows.Count > 0)
        {
            var calls = new List<string>();
            foreach (var ((callee, line), args) in CallArgFlows)
            {
                var argParts = args.Select(kv => $"{kv.Key}<-{string.Join(",", kv.Value.Select(o => o.ToString()).Distinct())}");
                calls.Add($"{callee}:{line}({string.Join(";", argParts)})");
            }
            parts.Add($"calls:{string.Join("|", calls)}");
        }
        return string.Join(" | ", parts);
    }
}

/// <summary>
/// Fast intra-procedural def-use analyzer computing parameter-to-call, parameter-to-return, and field mutation flows.
/// </summary>
public static class FunctionDataflowAnalyzer
{
    private static readonly AsyncLocal<bool?> _enabled = new();

    /// <summary>
    /// When true (default), computes intraprocedural dataflow summaries for function declarations.
    /// Can be disabled in lightweight AST scanning modes to avoid extra allocations and CPU overhead.
    /// </summary>
    public static bool Enabled
    {
        get => _enabled.Value ?? true;
        set => _enabled.Value = value;
    }

    public static FunctionDataflowSummary Analyze(FunctionDeclSyntax fn)
    {
        var summary = new FunctionDataflowSummary();
        if (!Enabled || (fn.Span.EndByte - fn.Span.StartByte) > 64 * 1024)
        {
            fn.DataflowSummary = summary;
            return summary;
        }
        var varOrigins = new Dictionary<string, List<DataflowOrigin>>(StringComparer.Ordinal);

        // 1. Seed parameters
        for (int i = 0; i < fn.Parameters.Count; i++)
        {
            var p = fn.Parameters[i];
            var pName = !string.IsNullOrEmpty(p.Name) ? p.Name : $"p{i}";
            var origin = new DataflowOrigin(FlowSourceKind.Parameter, $"p{i}");
            varOrigins[pName] = [origin];
        }

        // 2. Scan body statements or expression body
        if (fn.Body != null)
        {
            ScanBlock(fn.Body, varOrigins, summary);
        }
        else if (fn.ExpressionBody != null)
        {
            var origins = ResolveExprOrigins(fn.ExpressionBody, varOrigins);
            summary.ReturnOrigins.AddRange(origins);
            ScanExprCalls(fn.ExpressionBody, varOrigins, summary);
        }

        fn.DataflowSummary = summary;
        return summary;
    }

    private static void ScanBlock(BlockStmtSyntax block, Dictionary<string, List<DataflowOrigin>> varOrigins, FunctionDataflowSummary summary)
    {
        for (int i = 0; i < block.Children.Count; i++)
        {
            ScanNode(block.Children[i], varOrigins, summary);
        }
    }

    private static void ScanNode(SyntaxNode node, Dictionary<string, List<DataflowOrigin>> varOrigins, FunctionDataflowSummary summary)
    {
        switch (node)
        {
            case LambdaExprSyntax:
                return;

            case DeclarationSyntax decl:
                ProcessDeclaration(decl, varOrigins, summary);
                break;

            case AssignmentExprSyntax assign:
                HandleAssignment(assign, varOrigins, summary);
                break;

            case ReturnStmtSyntax ret when ret.Value != null:
                var retOrigins = ResolveExprOrigins(ret.Value, varOrigins);
                summary.ReturnOrigins.AddRange(retOrigins);
                ScanExprCalls(ret.Value, varOrigins, summary);
                break;

            case ExprSyntax expr:
                ScanExprCalls(expr, varOrigins, summary);
                break;

            default:
                for (int i = 0; i < node.Children.Count; i++)
                {
                    ScanNode(node.Children[i], varOrigins, summary);
                }
                break;
        }
    }

    private static void ProcessDeclaration(DeclarationSyntax decl, Dictionary<string, List<DataflowOrigin>> varOrigins, FunctionDataflowSummary summary)
    {
        if (decl is VariableDeclSyntax v)
        {
            var name = !string.IsNullOrEmpty(v.Name)
                ? v.Name
                : (v.GetChildForField(SyntaxFields.Name) ?? v.Children.FirstOrDefault(static c => c is IdentifierExprSyntax))?.Text;

            var init = v.Initializer ??
                       v.GetChildForField(SyntaxFields.Value) as ExprSyntax ??
                       v.Children.OfType<ExprSyntax>().LastOrDefault();

            if (!string.IsNullOrEmpty(name) && init != null)
            {
                var origins = ResolveExprOrigins(init, varOrigins);
                varOrigins[name] = origins;
                ScanExprCalls(init, varOrigins, summary);
            }

            foreach (var childDecl in v.Children.OfType<DeclarationSyntax>())
            {
                ProcessDeclaration(childDecl, varOrigins, summary);
            }
        }
    }

    private static void HandleAssignment(AssignmentExprSyntax assign, Dictionary<string, List<DataflowOrigin>> varOrigins, FunctionDataflowSummary summary)
    {
        if (assign.Right == null) return;
        var origins = ResolveExprOrigins(assign.Right, varOrigins);
        ScanExprCalls(assign.Right, varOrigins, summary);

        if (assign.Left is IdentifierExprSyntax id)
        {
            var idSpan = id.NameSpan;
            if (!idSpan.IsEmpty)
            {
                var spanLookup = varOrigins.GetAlternateLookup<ReadOnlySpan<char>>();
                spanLookup[idSpan] = origins;
            }
        }
        else if (assign.Left is MemberAccessExprSyntax mem)
        {
            if (mem.Object is IdentifierExprSyntax objId && objId.NameSpan is "this" or "self" && !mem.MemberNameSpan.IsEmpty)
            {
                var memberName = mem.MemberName;
                if (!summary.FieldWrites.TryGetValue(memberName, out var fw))
                {
                    fw = [];
                    summary.FieldWrites[memberName] = fw;
                }
                fw.AddRange(origins);
            }
        }
    }

    private static void ScanExprCalls(ExprSyntax expr, Dictionary<string, List<DataflowOrigin>> varOrigins, FunctionDataflowSummary summary)
    {
        if (expr is LambdaExprSyntax) return;
        if (expr is CallExprSyntax call)
        {
            RecordCall(call, varOrigins, summary);
        }

        foreach (var child in expr.Children.OfType<ExprSyntax>())
        {
            ScanExprCalls(child, varOrigins, summary);
        }
    }

    private static void RecordCall(CallExprSyntax call, Dictionary<string, List<DataflowOrigin>> varOrigins, FunctionDataflowSummary summary)
    {
        if (call.MethodNameSpan.IsEmpty) return;

        var key = (call.MethodName, call.Span.StartLine + 1);
        if (!summary.CallArgFlows.TryGetValue(key, out var argMap))
        {
            argMap = new Dictionary<int, List<DataflowOrigin>>();
            summary.CallArgFlows[key] = argMap;
        }

        for (int i = 0; i < call.Arguments.Count; i++)
        {
            var arg = call.Arguments[i];
            if (arg.Value != null)
            {
                var origins = ResolveExprOrigins(arg.Value, varOrigins);
                if (origins.Count > 0)
                {
                    if (!argMap.TryGetValue(i, out var list))
                    {
                        list = [];
                        argMap[i] = list;
                    }
                    list.AddRange(origins);
                }
            }
        }
    }

    private static List<DataflowOrigin> ResolveExprOrigins(ExprSyntax expr, Dictionary<string, List<DataflowOrigin>> varOrigins)
    {
        var list = new List<DataflowOrigin>();
        var spanLookup = varOrigins.GetAlternateLookup<ReadOnlySpan<char>>();

        switch (expr)
        {
            case IdentifierExprSyntax id:
                if (spanLookup.TryGetValue(id.NameSpan, out var found))
                {
                    list.AddRange(found);
                }
                break;

            case MemberAccessExprSyntax mem:
                if (mem.Object is IdentifierExprSyntax objId)
                {
                    var objSpan = objId.NameSpan;
                    if (objSpan is "this" or "self")
                    {
                        list.Add(new DataflowOrigin(FlowSourceKind.InstanceField, $"this.{mem.MemberName}"));
                    }
                    else if (spanLookup.TryGetValue(objSpan, out var objOrigins))
                    {
                        foreach (var orig in objOrigins)
                        {
                            var propPath = (orig.PropertyPath ?? "") + $".{mem.MemberName}";
                            list.Add(new DataflowOrigin(orig.Kind, orig.Identifier, propPath));
                        }
                    }
                }
                break;

            case LiteralExprSyntax lit:
                list.Add(new DataflowOrigin(FlowSourceKind.Constant, lit.ValueText ?? lit.Text));
                break;

            case BinaryExprSyntax bin:
                if (bin.Left != null) list.AddRange(ResolveExprOrigins(bin.Left, varOrigins));
                if (bin.Right != null) list.AddRange(ResolveExprOrigins(bin.Right, varOrigins));
                break;

            case CallExprSyntax call:
                list.Add(new DataflowOrigin(FlowSourceKind.CallReturn, call.MethodName));
                break;

            case UnaryExprSyntax unary when unary.Operand != null:
                list.AddRange(ResolveExprOrigins(unary.Operand, varOrigins));
                break;

            case ArgumentSyntax arg when arg.Value != null:
                list.AddRange(ResolveExprOrigins(arg.Value, varOrigins));
                break;
        }

        return list;
    }
}
