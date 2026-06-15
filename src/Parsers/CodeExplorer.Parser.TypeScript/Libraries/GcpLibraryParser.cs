using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;
using System;
using System.Collections.Generic;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GcpLibraryParser : ILibraryParser
{
    public string Type => "cloud";
    public string Name => "GCP";
    public string Id => "gcp";
    public IReadOnlyList<string> SupportedPatterns => ["@google-cloud", "@google-cloud/*", "firebase", "firebase-admin"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (node.Type == "call_expression")
        {
            var funcNode = node.GetChildForField("function");
            if (funcNode != null && funcNode.Id != IntPtr.Zero)
            {
                var funcText = funcNode.Text;

                if (funcText.EndsWith(".publishMessage", StringComparison.Ordinal) || funcText == "sendMessageToTopic")
                {
                    var argList = node.GetChildForField("arguments");
                    if (argList != null && argList.Children.Count > 1)
                    {
                        var firstArg = argList.Children[1];
                        var topicName = AstHelper.ResolveStringOrTemplate(firstArg);
                        if (!string.IsNullOrEmpty(topicName))
                        {
                            references.Add(new Reference(scopeSymbolId, "gcp:" + topicName, OntologyConstants.Relationships.PublishesTo));
                        }
                    }
                }
                else if (funcText.EndsWith(".publish", StringComparison.Ordinal) && funcNode.Type == "member_expression")
                {
                    var objCall = funcNode.GetChildForField("object");
                    if (objCall != null && objCall.Type == "call_expression")
                    {
                        var innerFunc = objCall.GetChildForField("function")?.Text;
                        if (innerFunc != null && innerFunc.EndsWith(".topic", StringComparison.Ordinal))
                        {
                            var argList = objCall.GetChildForField("arguments");
                            if (argList != null && argList.Children.Count > 1)
                            {
                                var firstArg = argList.Children[1];
                                var topicName = AstHelper.ResolveStringOrTemplate(firstArg);
                                if (!string.IsNullOrEmpty(topicName))
                                {
                                    references.Add(new Reference(scopeSymbolId, "gcp:" + topicName, OntologyConstants.Relationships.PublishesTo));
                                }
                            }
                        }
                    }
                }
                else if (funcText.EndsWith(".subscribeToMessages", StringComparison.Ordinal))
                {
                    var argList = node.GetChildForField("arguments");
                    if (argList != null && argList.Children.Count > 1)
                    {
                        var firstArg = argList.Children[1];
                        var topicName = AstHelper.ResolveStringOrTemplate(firstArg);
                        if (!string.IsNullOrEmpty(topicName))
                        {
                            references.Add(new Reference(scopeSymbolId, "gcp:" + topicName, OntologyConstants.Relationships.SubscribesTo));
                        }
                    }
                }
            }
        }
    }
}
