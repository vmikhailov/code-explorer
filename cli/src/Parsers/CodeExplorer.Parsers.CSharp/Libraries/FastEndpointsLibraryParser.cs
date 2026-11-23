using System.Collections.Frozen;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.CSharp.Libraries;

public class FastEndpointsLibraryParser : DeclarativeSemanticExtension
{
    private const string ConfigureMethodName = "Configure";
    private const string EndpointClassToken = "Endpoint";
    private static readonly FrozenDictionary<string, string> VerbMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Get"] = "GET",
        ["Post"] = "POST",
        ["Put"] = "PUT",
        ["Delete"] = "DELETE",
        ["Patch"] = "PATCH"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public FastEndpointsLibraryParser()
    {
        Id = "fastendpoints";
        Name = "FastEndpoints";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["FastEndpoints", "FastEndpoints.*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.CSharp.InvocationExpression }.ToFrozenSet(StringComparer.Ordinal),
                MapNodeType = static (node, ctx) => TryMatchFastEndpointVerb(node, out _, out _) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = static (node, ctx, projectName, relPath) =>
                    TryMatchFastEndpointVerb(node, out var verb, out var routeExpr)
                        ? $"{verb}:{ResolveRouteArg(routeExpr, projectName)}"
                        : null,
                CollectReferences = static (node, scopeSymbolId, references, _) =>
                {
                    if (TryMatchFastEndpointVerb(node, out var verb, out var routeExpr))
                    {
                        references.Add(new Reference(
                            scopeSymbolId,
                            $"{verb}:{ResolveRouteArg(routeExpr, null)}",
                            OntologyConstants.Relationships.Exposes));
                    }
                }
            }
        ];
    }

    private static bool TryMatchFastEndpointVerb(Node node, out string verb, out Node? routeArg)
    {
        verb = string.Empty;
        routeArg = null;
        if (node is not CallExprSyntax { Receiver: null } call) return false;
        var methodName = call.MethodNameSpan;
        if (methodName.IsEmpty || !VerbMap.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(methodName, out var mappedVerb))
        {
            return false;
        }

        // Must be inside Configure() of a FastEndpoints Endpoint class
        var inConfigure = false;
        var inEndpointClass = false;
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr is FunctionDeclSyntax fn && string.Equals(fn.Name, ConfigureMethodName, StringComparison.Ordinal))
            {
                inConfigure = true;
            }
            else if (curr is TypeDeclSyntax td && td.AsSpan().Contains(EndpointClassToken.AsSpan(), StringComparison.Ordinal))
            {
                inEndpointClass = true;
                break;
            }
            curr = curr.Parent;
        }

        if (!inConfigure || !inEndpointClass) return false;

        verb = mappedVerb;
        if (call.Arguments.Count > 0)
        {
            routeArg = call.Arguments[0];
        }
        return routeArg != null;
    }

    private static string ResolveRouteArg(Node? argNode, string? projectName)
    {
        if (!argNode.IsValid()) return "/";
        if (argNode is ArgumentSyntax { Value: { } val }) argNode = val;
        if (AstValueResolver.TryResolveString(argNode, projectName, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
        {
            var clean = resolved.Trim('"', '\'').Trim();
            return clean.StartsWith('/') ? clean : "/" + clean;
        }
        var raw = argNode.Text.Trim('"', '\'').Trim();
        return raw.StartsWith('/') ? raw : "/" + raw;
    }
}
