using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;
using System;
using System.Collections.Generic;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class SocketIoLibraryParser : ILibraryParser
{
    public string Name => "Socket.io";
    public string Id => "socketio";
    public string Type => "api";
    public IReadOnlyList<string> SupportedPatterns => ["socket.io", "socket.io-client"];
    public bool IsImplemented => true;

    private static readonly NodeSelector _socketOnSelector = NodeSelector.New()
        .HasType("call_expression")
        .FunctionNode
        .Where(NodeSelector.Or(
            NodeSelector.New().HasType("identifier").Text("on"),
            NodeSelector.New()
                .HasType("member_expression")
                .HasChild("property", NodeSelector.New().Text("on"))
        ));

    private static readonly NodeSelector _socketEmitSelector = NodeSelector.New()
        .HasType("call_expression")
        .FunctionNode
        .Where(NodeSelector.Or(
            NodeSelector.New().HasType("identifier").Text("emit"),
            NodeSelector.New()
                .HasType("member_expression")
                .HasChild("property", NodeSelector.New().Text("emit"))
        ));

    public IReadOnlyDictionary<string, NodeSelector> Selectors => new Dictionary<string, NodeSelector>
    {
        { OntologyConstants.NodeLabels.EntryPoint, _socketOnSelector },
        { OntologyConstants.NodeLabels.ExternalService, _socketEmitSelector }
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (_socketOnSelector.Matches(node)) return OntologyConstants.NodeLabels.EntryPoint;
        if (_socketEmitSelector.Matches(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        var isOn = _socketOnSelector.Matches(node);
        var isEmit = _socketEmitSelector.Matches(node);

        if (isOn || isEmit)
        {
            var argList = node.GetChildForField("arguments");
            if (argList != null && argList.Children.Count > 1)
            {
                var firstArg = argList.Children[1];
                var eventName = AstHelper.ResolveStringOrTemplate(firstArg);
                if (!string.IsNullOrEmpty(eventName))
                {
                    return $"ws:{eventName}";
                }
            }
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
    }
}
