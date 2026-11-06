using System.Collections.Frozen;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

public partial class DeclarativeSemanticExtension
{
    #region SQL Sink & ORM Table Helpers

    private static bool MatchesSqlSink(Node node, SqlSinkRule rule)
    {
        if (rule.AnnotationNodeTypes.Contains(node.Type) && rule.QueryAnnotationSuffixes.Count > 0)
        {
            var name = GetShortAnnotationName(node);
            if (!string.IsNullOrEmpty(name) &&
                rule.QueryAnnotationSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (rule.CallNodeTypes.Contains(node.Type))
        {
            var methodName = GetCallMethodName(node);
            if (!string.IsNullOrEmpty(methodName) && rule.CallMethodNames.Contains(methodName))
            {
                if (!rule.RequireExtractableSqlOnCall) return true;
                return !string.IsNullOrEmpty(ExtractSqlText(node));
            }
        }

        return false;
    }

    private static string? ExtractSqlText(Node node)
    {
        if (node is AnnotationSyntax || node.Type == "attribute_group")
        {
            var strNode = node.Children.FirstOrDefault(IsStringLiteral);
            if (strNode.IsValid())
            {
                return strNode.Text.Trim('"', '\'').Trim();
            }

            var pairs = new List<Node>();
            CollectNodesByType(node, "element_value_pair", pairs);
            foreach (var pair in pairs)
            {
                var key = pair.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                if (key.IsValid() && key.Text is "value" or "query" or "nativeQuery")
                {
                    var val = pair.Children.FirstOrDefault(IsStringLiteral);
                    if (val.IsValid()) return val.Text.Trim('"', '\'').Trim();
                }
            }

            var text = node.Text;
            var f = text.IndexOf('"');
            var l = text.LastIndexOf('"');
            if (f >= 0 && l > f) return text[(f + 1)..l].Trim();
        }

        var argList = FindArgumentsNode(node);
        if (argList.IsValid())
        {
            var firstStr = argList.Children.FirstOrDefault(IsStringLiteral);
            if (firstStr.IsValid())
            {
                return firstStr.Text.Trim('"', '\'').Trim();
            }
        }

        return null;
    }

    private static bool MatchesOrmTableAnnotation(Node node, OrmTableRule rule)
    {
        if (!rule.AnnotationNodeTypes.Contains(node.Type)) return false;
        var name = GetShortAnnotationName(node);
        if (string.IsNullOrEmpty(name) || !rule.TableAnnotationNames.Contains(name)) return false;
        var tableName = ExtractOrmTableName(node, rule);
        return !string.IsNullOrEmpty(tableName) && !tableName.Equals("Entity", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractOrmTableName(Node annotNode, OrmTableRule rule)
    {
        return ExtractNamedOrFirstStringFromAnnotation(annotNode, rule.NameArgumentKeys);
    }

    #endregion

    #region Messaging Helpers

    private static bool MatchesMessagingListenerAnnotation(Node node, MessagingBrokerRule rule)
    {
        if (rule.ListenerAnnotationNames.Count == 0 || !rule.AnnotationNodeTypes.Contains(node.Type))
            return false;

        var name = GetShortAnnotationName(node);
        return !string.IsNullOrEmpty(name) && rule.ListenerAnnotationNames.Contains(name);
    }

    private static bool MatchesMethodLevelEventListener(Node node, MessagingBrokerRule rule)
    {
        if (rule.MethodLevelEventAnnotations.Count == 0 || node is not FunctionDeclSyntax)
            return false;

        var annotations = GetNodeAnnotations(node, rule.AnnotationNodeTypes);
        return annotations.Any(a =>
        {
            var name = GetShortAnnotationName(a);
            return !string.IsNullOrEmpty(name) && rule.MethodLevelEventAnnotations.Contains(name);
        });
    }

    private static List<string> ExtractMessagingTopics(Node annotNode, MessagingBrokerRule rule)
    {
        var list = new List<string>();

        foreach (var child in annotNode.Children)
        {
            if (child.Type is "annotation_argument_list" or "arguments")
            {
                foreach (var argChild in child.Children)
                {
                    if (argChild.Type == "element_value_pair")
                    {
                        var keyNode = argChild.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                        if (keyNode.IsValid() && rule.TopicArgumentKeys.Contains(keyNode.Text))
                        {
                            ExtractStringsFromSubtree(argChild, list);
                        }
                    }
                    else if (IsStringLiteral(argChild))
                    {
                        var val = argChild.Text.Trim('"', '\'');
                        if (!string.IsNullOrEmpty(val) && !list.Contains(val)) list.Add(val);
                    }
                }
            }
        }

        if (list.Count == 0)
        {
            ExtractStringsFromSubtree(annotNode, list);
        }

        if (list.Count == 0)
        {
            var text = annotNode.Text;
            var first = text.IndexOf('"');
            var last = text.LastIndexOf('"');
            if (first >= 0 && last > first)
            {
                list.Add(text[(first + 1)..last]);
            }
        }

        return list;
    }

    private static void TryCollectMessagingPublishReferences(
        Node callNode,
        string scopeSymbolId,
        List<Reference> references,
        MessagingBrokerRule rule)
    {
        var methodName = GetCallMethodName(callNode);
        if (string.IsNullOrEmpty(methodName) || !rule.PublishMethodNames.Contains(methodName))
            return;

        if (rule.PublishReceiverSubstrings.Count > 0)
        {
            var objNode = callNode.GetField(SyntaxFields.Object)
                          ?? callNode.GetField(SyntaxFields.Operand)
                          ?? callNode.GetField("scope");
            var objText = objNode.IsValid() ? objNode.Text : "";
            if (!rule.PublishReceiverSubstrings.Any(sub => objText.Contains(sub, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
        }

        var argList = FindArgumentsNode(callNode);
        if (!argList.IsValid()) return;

        if (rule.ExtractEventTypeFromObjectCreationArg)
        {
            var objCreation = argList.Children.FirstOrDefault(c => c is CallExprSyntax { CallKind: CallSyntaxKind.Constructor } || c.Type == "object_creation_expression");
            if (objCreation.IsValid())
            {
                var typeNode = objCreation.GetField(SyntaxFields.Type)
                               ?? objCreation.Children.FirstOrDefault(c => c is IdentifierExprSyntax or TypeRefSyntax);
                if (typeNode.IsValid())
                {
                    references.Add(new Reference(scopeSymbolId, $"{rule.Protocol}:{typeNode.Text}", OntologyConstants.Relationships.PublishesTo));
                }
            }
            return;
        }

        var args = argList.Children.Where(c => c.Type is not "(" and not ")" and not ",").ToList();
        if (args.Count == 0) return;

        string? target = null;
        if (rule.PreferSecondArgWhenThreeOrMoreArgs && args.Count >= 3)
        {
            target = CleanPublishTargetArg(args[1]) ?? CleanPublishTargetArg(args[0]);
        }
        else
        {
            target = CleanPublishTargetArg(args[0]);
        }

        if (!string.IsNullOrEmpty(target))
        {
            references.Add(new Reference(scopeSymbolId, $"{rule.Protocol}:{target}", OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static string? CleanPublishTargetArg(Node argNode)
    {
        if (IsStringLiteral(argNode))
        {
            return argNode.Text.Trim('"', '\'');
        }

        if (argNode is CallExprSyntax { CallKind: CallSyntaxKind.Constructor } || argNode.Type == "object_creation_expression")
        {
            var innerArgs = FindArgumentsNode(argNode);
            if (innerArgs.IsValid())
            {
                var firstInner = innerArgs.Children.FirstOrDefault(c => c.Type is not "(" and not ")" and not ",");
                if (firstInner.IsValid())
                {
                    return CleanPublishTargetArg(firstInner);
                }
            }
        }

        if (argNode is IdentifierExprSyntax or MemberAccessExprSyntax)
        {
            var text = argNode.Text.Trim('"', '\'');
            if (text.Contains('.')) text = text[(text.LastIndexOf('.') + 1)..];
            return text;
        }

        return null;
    }

    #endregion

    #region Shared AST Utility Helpers

    private static string? GetShortAnnotationName(Node annot)
    {
        var nameNode = annot.GetField(SyntaxFields.Name)
                       ?? annot.Children.FirstOrDefault(c => c is IdentifierExprSyntax or MemberAccessExprSyntax)
                       ?? annot.Children.FirstOrDefault(c => c.Type.Contains("identifier", StringComparison.Ordinal) || c.Type.Contains("name", StringComparison.Ordinal));
        if (!nameNode.IsValid()) return null;

        var name = nameNode.Text.TrimStart('@', '#');
        if (name.Contains('.')) name = name[(name.LastIndexOf('.') + 1)..];
        if (name.Contains('\\')) name = name[(name.LastIndexOf('\\') + 1)..];
        return name;
    }

    private static string? GetCallMethodName(Node callNode)
    {
        var nameNode = callNode.GetField(SyntaxFields.Name)
                       ?? callNode.GetField(SyntaxFields.Field)
                       ?? callNode.GetField("member");
        if (nameNode.IsValid()) return nameNode.Text;

        var funcNode = callNode.GetField(SyntaxFields.Function);
        if (funcNode.IsValid())
        {
            var field = funcNode.GetField(SyntaxFields.Field) ?? funcNode.GetField(SyntaxFields.Name);
            if (field.IsValid()) return field.Text;
            return funcNode.Text;
        }

        return null;
    }

    private static Node? FindArgumentsNode(Node callNode)
    {
        return callNode.GetField(SyntaxFields.Arguments)
               ?? callNode.FindChildOfType("argument_list")
               ?? callNode.FindChildOfType("arguments")
               ?? callNode.Children.FirstOrDefault(c => c.Type.Contains("argument", StringComparison.Ordinal));
    }

    private static bool IsStringLiteral(Node node)
    {
        return node.IsValid() &&
               (node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.RawString } ||
                node.IsAny("string", "encapsed_string") ||
                node.Type.Contains("string_literal", StringComparison.Ordinal));
    }

    private static List<Node> GetNodeAnnotations(Node declNode, FrozenSet<string> annotationNodeTypes)
    {
        var result = new List<Node>();
        foreach (var child in declNode.Children)
        {
            if (!child.IsValid()) continue;
            if (annotationNodeTypes.Contains(child.Type))
            {
                result.Add(child);
            }
            else if (child.Type is "modifiers" or "attribute_list" or "decorator_list")
            {
                foreach (var modChild in child.Children)
                {
                    if (modChild.IsValid() && annotationNodeTypes.Contains(modChild.Type))
                    {
                        result.Add(modChild);
                    }
                }
            }
        }
        return result;
    }

    private static string? ExtractNamedOrFirstStringFromAnnotation(Node annotNode, FrozenSet<string> acceptedKeys)
    {
        foreach (var child in annotNode.Children)
        {
            if (child.Type == "element_value_pair")
            {
                var key = child.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                if (key.IsValid() && acceptedKeys.Contains(key.Text))
                {
                    var val = child.Children.FirstOrDefault(IsStringLiteral);
                    if (val.IsValid()) return val.Text.Trim('"', '\'');
                }
            }
            else if (child.Type is "annotation_argument_list" or "arguments")
            {
                foreach (var argChild in child.Children)
                {
                    if (argChild.Type is "element_value_pair" or "argument")
                    {
                        var key = argChild.Children.FirstOrDefault(c => c is IdentifierExprSyntax) ?? argChild.GetField(SyntaxFields.Name);
                        if (key.IsValid() && acceptedKeys.Contains(key.Text))
                        {
                            var val = argChild.Children.FirstOrDefault(IsStringLiteral);
                            if (val.IsValid()) return val.Text.Trim('"', '\'');
                        }
                    }
                    else if (IsStringLiteral(argChild))
                    {
                        return argChild.Text.Trim('"', '\'');
                    }
                }
            }
            else if (IsStringLiteral(child))
            {
                return child.Text.Trim('"', '\'');
            }
        }

        var text = annotNode.Text;
        if (text.Contains('"'))
        {
            var f = text.IndexOf('"');
            var l = text.LastIndexOf('"');
            if (l > f) return text[(f + 1)..l];
        }
        return null;
    }

    private static void ExtractStringsFromSubtree(Node node, List<string> results)
    {
        if (IsStringLiteral(node))
        {
            var clean = node.Text.Trim('"', '\'');
            if (!string.IsNullOrEmpty(clean) && !results.Contains(clean))
            {
                results.Add(clean);
            }
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractStringsFromSubtree(child, results);
        }
    }

    private static void CollectNodesByType(Node node, string nodeType, List<Node> result)
    {
        if (node.Is(nodeType))
        {
            result.Add(node);
        }
        foreach (var child in node.Children)
        {
            CollectNodesByType(child, nodeType, result);
        }
    }

    private static string? FindEnclosingMethodName(Node annotNode)
    {
        var current = annotNode.Parent;
        while (current.IsValid())
        {
            if (current is FunctionDeclSyntax fn)
            {
                if (!string.IsNullOrEmpty(fn.Name)) return fn.Name;
                var nameField = current.GetField(SyntaxFields.Name);
                if (nameField.IsValid()) return nameField.Text;

                foreach (var child in current.Children)
                {
                    if (child is IdentifierExprSyntax) return child.Text;
                }
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? ExtractFirstParameterType(Node methodNode)
    {
        if (methodNode is FunctionDeclSyntax { Parameters.Count: > 0 } fn && fn.Parameters[0].ParameterType is { } pt)
        {
            return pt.Text;
        }

        var paramList = methodNode.GetField(SyntaxFields.Parameters)
                        ?? methodNode.Children.FirstOrDefault(c => c.Type.Contains("parameters", StringComparison.Ordinal));
        if (!paramList.IsValid()) return null;

        var firstParam = paramList.Children.FirstOrDefault(c => c is ParameterDeclSyntax || c.Type.Contains("parameter", StringComparison.Ordinal));
        if (!firstParam.IsValid()) return null;

        var typeNode = firstParam.GetField(SyntaxFields.Type);
        return typeNode.IsValid() ? typeNode.Text : null;
    }

    #endregion
}
