using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

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
        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var funcNode = node.GetFunctionNode();
            if (funcNode.IsValid())
            {
                var funcText = funcNode.Text;

                if (funcText.EndsWith(".publishMessage", StringComparison.Ordinal) || funcText == "sendMessageToTopic")
                {
                    var argList = node.GetField(TreeSitterSyntax.Fields.Arguments);
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
                else if (funcText.EndsWith(".publish", StringComparison.Ordinal) && funcNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                {
                    var objCall = funcNode.GetField(TreeSitterSyntax.Fields.Object);
                    if (objCall.IsValid() && objCall.Is(TreeSitterSyntax.TypeScript.CallExpression))
                    {
                        var innerFunc = objCall.GetChildForField(TreeSitterSyntax.Fields.Function)?.Text;
                        if (innerFunc != null && innerFunc.EndsWith(".topic", StringComparison.Ordinal))
                        {
                            var argList = objCall.GetChildForField(TreeSitterSyntax.Fields.Arguments);
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
                    var argList = node.GetChildForField(TreeSitterSyntax.Fields.Arguments);
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
