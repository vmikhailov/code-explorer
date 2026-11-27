using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Parser.Layers;

internal static class CallSiteReferenceEnricher
{
    internal static void EnrichReferencesWithCallContextAndFlow(IOntologyNode rootNode, SyntaxDocument doc)
    {
        var callsByLineAndName = new Dictionary<(int Line, string MethodName), CallExprSyntax>();
        var callsByLine = new Dictionary<int, List<CallExprSyntax>>();

        foreach (var call in doc.AllCalls)
        {
            var line = call.Span.StartLine + 1;
            if (!string.IsNullOrEmpty(call.MethodName)) callsByLineAndName.TryAdd((line, call.MethodName), call);
            if (!callsByLine.TryGetValue(line, out var list)) callsByLine[line] = list = [];
            list.Add(call);
        }

        if (callsByLine.Count == 0) return;

        void WalkAndEnrich(IOntologyNode node)
        {
            for (var i = 0; i < node.References.Count; i++)
            {
                var r = node.References[i];
                if (r.Line <= 0 || r.Kind != RelationshipKind.Calls) continue;

                var targetMethod = r.TargetName.Contains('.') ? r.TargetName[(r.TargetName.LastIndexOf('.') + 1)..] : r.TargetName;
                CallExprSyntax? matchedCall = null;
                if (!string.IsNullOrEmpty(targetMethod) && callsByLineAndName.TryGetValue((r.Line, targetMethod), out var namedCall))
                    matchedCall = namedCall;
                else if (callsByLine.TryGetValue(r.Line, out var list) && list.Count > 0)
                    matchedCall = list.FirstOrDefault(c => c.MethodName == targetMethod) ?? list[0];

                if (matchedCall == null) continue;

                var context = CallSiteContextExtractor.Extract(matchedCall);
                string? argFlow = null;
                SyntaxNode? curr = matchedCall.Parent;
                while (curr is not null and not FunctionDeclSyntax) curr = curr.Parent;

                if (curr is FunctionDeclSyntax { DataflowSummary: not null } fnDecl &&
                    fnDecl.DataflowSummary.CallArgFlows.TryGetValue((matchedCall.MethodName, matchedCall.Span.StartLine + 1), out var argMap) &&
                    argMap.Count > 0)
                {
                    argFlow = string.Join(";", argMap.Select(kv => $"{kv.Key}<-{string.Join(",", kv.Value.Select(o => o.ToString()))}"));
                }

                if (string.IsNullOrEmpty(argFlow) && matchedCall.Arguments.Count > 0)
                {
                    var directArgParts = new List<string>();
                    for (var argIdx = 0; argIdx < matchedCall.Arguments.Count; argIdx++)
                    {
                        var argVal = matchedCall.Arguments[argIdx].Value;
                        if (argVal is IdentifierExprSyntax { IsSelfOrSuper: false } argId && !argId.NameSpan.IsEmpty)
                            directArgParts.Add($"{argIdx}<-{argId.Name}");
                        else if (argVal is LiteralExprSyntax argLit && !string.IsNullOrEmpty(argLit.ValueText ?? argLit.Text))
                            directArgParts.Add($"{argIdx}<-{argLit.ValueText ?? argLit.Text}");
                    }
                    if (directArgParts.Count > 0) argFlow = string.Join(";", directArgParts);
                }

                var effectiveArgFlow = string.Equals(r.ArgFlow, "callback_arg", StringComparison.Ordinal) ? r.ArgFlow : argFlow;
                node.References[i] = r with { Context = context, ArgFlow = effectiveArgFlow };
            }

            foreach (var child in node.Children)
            {
                if (child.References.Count > 0 || child.Children.Count > 0) WalkAndEnrich(child);
            }
        }

        WalkAndEnrich(rootNode);
    }
}
