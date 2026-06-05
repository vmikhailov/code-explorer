using System;
using System.Collections.Generic;
using System.Linq;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class FlurlLibraryParser : ILibraryParser
{
    public string Name => "FlurlLibraryParser";

    public string Category => "api";

    public IEnumerable<string> SupportedLibraries => new[] { "Flurl", "Flurl.Http" };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsFlurlCall(node))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsFlurlCall(node))
        {
            var rootUrl = ExtractFlurlRootUrl(node);
            if (!string.IsNullOrEmpty(rootUrl))
            {
                if (Uri.TryCreate(rootUrl, UriKind.Absolute, out var uri))
                {
                    return uri.Host;
                }
                return rootUrl;
            }
            return "Flurl Call";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Flurl calls represent external services, no custom inner references are required
    }

    private static bool IsFlurlCall(Node node)
    {
        if (node.Type != "invocation_expression") return false;

        var func = node.GetChildForField("function");
        if (func == null || (func.Id == IntPtr.Zero && node.Children.Count > 0)) func = node.Children[0];
        if (func == null || func.Id == IntPtr.Zero) return false;

        if (func.Type == "member_access_expression")
        {
            var nameChild = func.GetChildForField("name");
            if (nameChild != null && nameChild.Id != IntPtr.Zero)
            {
                var methodName = nameChild.Text;
                return methodName is "GetAsync" or "PostAsync" or "PutAsync" or "DeleteAsync" or "PatchAsync"
                                   or "GetJsonAsync" or "PostJsonAsync" or "PutJsonAsync" or "DeleteJsonAsync" or "PatchJsonAsync"
                                   or "GetStringAsync" or "GetStreamAsync" or "GetXmlAsync" or "PostUrlEncodedAsync";
            }
        }
        return false;
    }

    private static string? ExtractFlurlRootUrl(Node node)
    {
        var current = node;
        while (current != null)
        {
            if (current.Type == "invocation_expression")
            {
                var func = current.GetChildForField("function");
                if (func == null || (func.Id == IntPtr.Zero && current.Children.Count > 0)) func = current.Children[0];
                if (func != null && func.Type == "member_access_expression")
                {
                    current = func.GetChildForField("expression");
                    continue;
                }
            }
            if (current.Type == "member_access_expression")
            {
                current = current.GetChildForField("expression");
                continue;
            }
            break;
        }

        if (current != null)
        {
            if (current.Type == "string_literal" || current.Type == "verbatim_string_literal")
            {
                return current.Text.Trim('"');
            }
            return current.Text;
        }
        return null;
    }
}
