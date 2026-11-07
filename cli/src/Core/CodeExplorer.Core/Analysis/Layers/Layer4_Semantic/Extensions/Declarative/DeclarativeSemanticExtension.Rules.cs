using System.Collections.Frozen;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

public partial class DeclarativeSemanticExtension
{
    #region SQL Sink & ORM Table Helpers

    internal static bool MatchesSqlSink(Node node, SqlSinkRule rule)
    {
        if ((node is AnnotationSyntax || rule.AnnotationNodeTypes.Contains(node.Type)) && rule.QueryAnnotationSuffixes.Count > 0)
        {
            var name = GetShortAnnotationName(node);
            if (!string.IsNullOrEmpty(name) &&
                rule.QueryAnnotationSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (DeclarativeUniversalRuleEvaluator.IsCallNode(node, rule.CallNodeTypes))
        {
            var methodSpan = GetCallMethodSpan(node);
            if (!methodSpan.IsEmpty && rule.CallMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan))
            {
                if (!rule.RequireExtractableSqlOnCall) return true;
                return !string.IsNullOrEmpty(ExtractSqlText(node));
            }
        }

        return false;
    }

    internal static string? ExtractSqlText(Node node)
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

        var direct = DeclarativeUniversalRuleEvaluator.ExtractStringArgAtOrFirst(node, 0);
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        if (node is CallExprSyntax { Arguments.Count: > 0 } callExpr)
        {
            var maxArgs = Math.Min(callExpr.Arguments.Count, 3);
            for (var i = 0; i < maxArgs; i++)
            {
                var resolved = TryResolveSqlArgumentNode(callExpr.Arguments[i]);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }
        }
        else
        {
            var argList = FindArgumentsNode(node);
            if (argList.IsValid())
            {
                var checkedCount = 0;
                for (var i = 0; i < argList.Children.Count && checkedCount < 3; i++)
                {
                    var child = argList.Children[i];
                    if (child.Type is "(" or ")" or ",") continue;
                    checkedCount++;
                    var resolved = TryResolveSqlArgumentNode(child);
                    if (!string.IsNullOrEmpty(resolved)) return resolved;
                }
            }
        }

        return null;
    }

    private static string? TryResolveSqlArgumentNode(Node argNode)
    {
        if (!argNode.IsValid()) return null;
        if (argNode is ArgumentSyntax { Value: { } argVal })
        {
            argNode = argVal;
        }
        var rawSpan = argNode.AsSpan().Trim();
        if (rawSpan.Equals("ctx", StringComparison.OrdinalIgnoreCase) ||
            rawSpan.StartsWith("context.", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (AstValueResolver.TryResolveString(argNode, null, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
        {
            return resolved.Trim('"', '\'', '`').Trim();
        }
        if (ConstantRegistry.TryResolve(null, rawSpan.ToString(), out var constVal) && !string.IsNullOrWhiteSpace(constVal))
        {
            return constVal.Trim('"', '\'', '`').Trim();
        }
        return null;
    }

    private const string NameEntity = "Entity";
    private const string NameDocument = "Document";

    internal static bool MatchesOrmTableAnnotation(Node node, OrmTableRule rule)
    {
        if (!rule.AnnotationNodeTypes.Contains(node.Type)) return false;
        var name = GetShortAnnotationName(node);
        if (string.IsNullOrEmpty(name) || !rule.TableAnnotationNames.Contains(name)) return false;

        // If the annotation has no explicit name argument, check whether the class has another annotation that defines an explicit table name
        var explicitName = ExtractNamedOrFirstStringFromAnnotation(node, rule.NameArgumentKeys);
        if (string.IsNullOrEmpty(explicitName))
        {
            var parent = node.Parent;
            while (parent.IsValid())
            {
                if (rule.ClassDeclarationNodeTypes.Contains(parent.Type))
                {
                    var classAnnots = GetNodeAnnotations(parent, rule.AnnotationNodeTypes);
                    if (classAnnots.Any(a => a != node &&
                        GetShortAnnotationName(a) is { } aName &&
                        rule.TableAnnotationNames.Contains(aName) &&
                        !string.IsNullOrEmpty(ExtractNamedOrFirstStringFromAnnotation(a, rule.NameArgumentKeys))))
                    {
                        return false;
                    }
                    break;
                }
                parent = parent.Parent;
            }
        }

        var tableName = ExtractOrmTableName(node, rule);
        return !string.IsNullOrEmpty(tableName) &&
               !tableName.Equals(NameEntity, StringComparison.OrdinalIgnoreCase) &&
               !tableName.Equals(NameDocument, StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ExtractOrmTableName(Node annotNode, OrmTableRule rule)
    {
        var name = ExtractNamedOrFirstStringFromAnnotation(annotNode, rule.NameArgumentKeys);
        if (!string.IsNullOrEmpty(name)) return name;

        // If no explicit table/collection name is given in the annotation, fall back to enclosing class name
        var parent = annotNode.Parent;
        while (parent.IsValid())
        {
            if (parent is TypeDeclSyntax tds && !string.IsNullOrEmpty(tds.Name))
            {
                return tds.Name;
            }
            if (rule.ClassDeclarationNodeTypes.Contains(parent.Type))
            {
                var idNode = parent.GetField(SyntaxFields.Name) ?? parent.Children.FirstOrDefault(c => c is IdentifierExprSyntax || c.Type == "identifier");
                if (idNode.IsValid() && !string.IsNullOrEmpty(idNode.Text))
                {
                    return idNode.Text;
                }
                break;
            }
            parent = parent.Parent;
        }

        return null;
    }

    #endregion

    #region Messaging Helpers

    internal static bool MatchesMessagingListenerAnnotation(Node node, MessagingBrokerRule rule)
    {
        if (rule.ListenerAnnotationNames.Count == 0 || !rule.AnnotationNodeTypes.Contains(node.Type))
            return false;

        var name = GetShortAnnotationName(node);
        return !string.IsNullOrEmpty(name) && rule.ListenerAnnotationNames.Contains(name);
    }

    internal static bool MatchesMessagingProducerAnnotation(Node node, MessagingBrokerRule rule)
    {
        if (rule.ProducerAnnotationNames.Count == 0 || !rule.AnnotationNodeTypes.Contains(node.Type))
            return false;

        var name = GetShortAnnotationName(node);
        return !string.IsNullOrEmpty(name) && rule.ProducerAnnotationNames.Contains(name);
    }

    internal static bool MatchesMessagingChannelAnnotation(Node node, MessagingBrokerRule rule)
    {
        if (rule.ChannelAnnotationNames.Count == 0 || !rule.AnnotationNodeTypes.Contains(node.Type))
            return false;

        var name = GetShortAnnotationName(node);
        return !string.IsNullOrEmpty(name) && rule.ChannelAnnotationNames.Contains(name);
    }

    internal static bool MatchesMethodLevelEventListener(Node node, MessagingBrokerRule rule)
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

    internal static List<string> ExtractMessagingTopics(Node annotNode, MessagingBrokerRule rule)
    {
        var list = new List<string>();

        foreach (var child in annotNode.Children)
        {
            if (child is SyntaxList { Kind: SyntaxListKind.Arguments } || child.Type is "annotation_argument_list" or "arguments")
            {
                foreach (var argChild in child.Children)
                {
                    if (argChild is KeyValueExprSyntax || argChild.Type == "element_value_pair")
                    {
                        var keyNode = argChild.Children.FirstOrDefault(c => c is IdentifierExprSyntax || c.Type == "identifier");
                        if (keyNode.IsValid() && rule.TopicArgumentKeys.Contains(keyNode.Text))
                        {
                            ExtractStringsFromSubtree(argChild, list);
                            if (list.Count == 0)
                            {
                                var argValId = argChild.Children.FirstOrDefault(c => c != keyNode && (c is IdentifierExprSyntax || c.Type == "identifier"));
                                if (argValId.IsValid())
                                {
                                    var resolved = ResolveConstantInScope(annotNode, argValId.Text);
                                    list.Add(resolved ?? argValId.Text);
                                }
                            }
                        }
                    }
                    else if (IsStringLiteral(argChild))
                    {
                        var val = argChild.Text.Trim('"', '\'');
                        if (!string.IsNullOrEmpty(val) && !list.Contains(val)) list.Add(val);
                    }
                    else if (argChild is IdentifierExprSyntax || argChild.Type == "identifier")
                    {
                        var constName = argChild.Text;
                        if (!string.IsNullOrEmpty(constName))
                        {
                            var resolved = ResolveConstantInScope(annotNode, constName);
                            list.Add(resolved ?? constName);
                        }
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

    private static string? ResolveConstantInScope(Node annotNode, string constName)
    {
        var classNode = annotNode.Parent;
        while (classNode.IsValid() && !classNode.IsAny(TypeDeclNodeTypes))
        {
            classNode = classNode.Parent;
        }
        return classNode.IsValid() ? ResolveConstantValueInClass(classNode, constName) : null;
    }

    private const string FieldDeclarationKind = "field_declaration";
    private const string VariableDeclaratorKind = "variable_declarator";
    private const string IdentifierKind = "identifier";

    private static string? ResolveConstantValueInClass(Node classNode, string constName)
    {
        var fields = new List<Node>();
        CollectNodesByType(classNode, FieldDeclarationKind, fields);
        foreach (var field in fields)
        {
            var decls = new List<Node>();
            CollectNodesByType(field, VariableDeclaratorKind, decls);
            foreach (var decl in decls)
            {
                var id = decl.Children.FirstOrDefault(c => c is IdentifierExprSyntax || c.Type == IdentifierKind);
                if (id.IsValid() && id.Text == constName)
                {
                    var val = decl.Children.FirstOrDefault(IsStringLiteral);
                    if (val.IsValid())
                    {
                        return val.Text.Trim('"', '\'');
                    }
                }
            }
        }
        return null;
    }

    internal static void TryCollectMessagingPublishReferences(
        Node callNode,
        ReadOnlySpan<char> methodSpan,
        string scopeSymbolId,
        List<Reference> references,
        MessagingBrokerRule rule)
    {
        if (rule.PublishMethodNames.Count == 0) return;

        if (methodSpan.IsEmpty) methodSpan = GetCallMethodSpan(callNode);
        if (methodSpan.IsEmpty || !rule.PublishMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan))
            return;

        if (rule.PublishReceiverSubstrings.Count > 0)
        {
            var objSpan = GetCallReceiverSpan(callNode);
            if (!DeclarativeUniversalRuleEvaluator.SpanContainsAny(objSpan, rule.PublishReceiverSubstrings))
            {
                return;
            }
        }

        if (!rule.ExtractEventTypeFromObjectCreationArg && callNode is CallExprSyntax { Arguments.Count: > 0 } callExpr)
        {
            var directCount = callExpr.Arguments.Count;
            string? directTarget = null;
            if (rule.PublishTopicArgumentIndex > 0 && directCount > rule.PublishTopicArgumentIndex)
            {
                directTarget = CleanPublishTargetArg(callExpr.Arguments[rule.PublishTopicArgumentIndex]);
            }
            else if (rule.PreferSecondArgWhenThreeOrMoreArgs && directCount >= 3)
            {
                directTarget = CleanPublishTargetArg(callExpr.Arguments[1]) ?? CleanPublishTargetArg(callExpr.Arguments[0]);
            }
            else
            {
                directTarget = CleanPublishTargetArg(callExpr.Arguments[0]);
            }

            directTarget ??= rule.StaticPublishTopic;
            if (!string.IsNullOrEmpty(directTarget))
            {
                references.Add(new Reference(scopeSymbolId, DeclarativeUniversalRuleEvaluator.FormatBrokerTopic(rule.Protocol, directTarget), OntologyConstants.Relationships.PublishesTo));
            }
            return;
        }

        var argList = FindArgumentsNode(callNode);
        if (!argList.IsValid())
        {
            if (!string.IsNullOrEmpty(rule.StaticPublishTopic))
            {
                references.Add(new Reference(scopeSymbolId, DeclarativeUniversalRuleEvaluator.FormatBrokerTopic(rule.Protocol, rule.StaticPublishTopic), OntologyConstants.Relationships.PublishesTo));
            }
            return;
        }

        if (rule.ExtractEventTypeFromObjectCreationArg)
        {
            var objCreation = argList.Children.FirstOrDefault(c => c is CallExprSyntax { CallKind: CallSyntaxKind.Constructor } || c.Type == ObjectCreationExprKind);
            if (objCreation.IsValid())
            {
                var typeNode = objCreation.GetField(SyntaxFields.Type)
                               ?? objCreation.Children.FirstOrDefault(c => c is IdentifierExprSyntax or TypeRefSyntax);
                if (typeNode.IsValid())
                {
                    references.Add(new Reference(scopeSymbolId, DeclarativeUniversalRuleEvaluator.FormatBrokerTopic(rule.Protocol, typeNode.Text), OntologyConstants.Relationships.PublishesTo));
                }
            }
            return;
        }

        Node? arg0 = null;
        Node? arg1 = null;
        Node? argAtTargetIdx = null;
        var argCount = 0;
        for (var i = 0; i < argList.Children.Count; i++)
        {
            var c = argList.Children[i];
            if (c.Type is "(" or ")" or ",") continue;
            if (argCount == 0) arg0 = c;
            else if (argCount == 1) arg1 = c;
            if (argCount == rule.PublishTopicArgumentIndex) argAtTargetIdx = c;
            argCount++;
        }

        if (argCount == 0)
        {
            if (!string.IsNullOrEmpty(rule.StaticPublishTopic))
            {
                references.Add(new Reference(scopeSymbolId, DeclarativeUniversalRuleEvaluator.FormatBrokerTopic(rule.Protocol, rule.StaticPublishTopic), OntologyConstants.Relationships.PublishesTo));
            }
            return;
        }

        string? target = null;
        if (rule.PublishTopicArgumentIndex > 0 && argAtTargetIdx != null)
        {
            target = CleanPublishTargetArg(argAtTargetIdx);
        }
        else if (rule.PreferSecondArgWhenThreeOrMoreArgs && argCount >= 3 && arg1 != null && arg0 != null)
        {
            target = CleanPublishTargetArg(arg1) ?? CleanPublishTargetArg(arg0);
        }
        else if (arg0 != null)
        {
            target = CleanPublishTargetArg(arg0);
        }

        target ??= rule.StaticPublishTopic;
        if (!string.IsNullOrEmpty(target))
        {
            references.Add(new Reference(scopeSymbolId, DeclarativeUniversalRuleEvaluator.FormatBrokerTopic(rule.Protocol, target), OntologyConstants.Relationships.PublishesTo));
        }
    }

    internal static void TryCollectMessagingSubscribeReferences(
        Node callNode,
        ReadOnlySpan<char> methodSpan,
        string scopeSymbolId,
        List<Reference> references,
        MessagingBrokerRule rule)
    {
        if (rule.SubscribeMethodNames.Count == 0) return;

        if (methodSpan.IsEmpty) methodSpan = GetCallMethodSpan(callNode);
        if (methodSpan.IsEmpty || !rule.SubscribeMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan))
            return;

        if (rule.SubscribeReceiverSubstrings.Count > 0)
        {
            var objSpan = GetCallReceiverSpan(callNode);
            if (!DeclarativeUniversalRuleEvaluator.SpanContainsAny(objSpan, rule.SubscribeReceiverSubstrings))
            {
                return;
            }
        }

        if (callNode is CallExprSyntax { Arguments.Count: > 0 } subCallExpr)
        {
            var directSubTarget = CleanPublishTargetArg(subCallExpr.Arguments[0]);
            if (!string.IsNullOrEmpty(directSubTarget))
            {
                references.Add(new Reference(scopeSymbolId, DeclarativeUniversalRuleEvaluator.FormatBrokerTopic(rule.Protocol, directSubTarget), OntologyConstants.Relationships.SubscribesTo));
            }
            return;
        }

        var argList = FindArgumentsNode(callNode);
        if (!argList.IsValid()) return;

        Node? firstArg = null;
        for (var i = 0; i < argList.Children.Count; i++)
        {
            var c = argList.Children[i];
            if (c.Type is "(" or ")" or ",") continue;
            firstArg = c;
            break;
        }
        if (firstArg == null) return;

        var target = CleanPublishTargetArg(firstArg);
        if (!string.IsNullOrEmpty(target))
        {
            references.Add(new Reference(scopeSymbolId, DeclarativeUniversalRuleEvaluator.FormatBrokerTopic(rule.Protocol, target), OntologyConstants.Relationships.SubscribesTo));
        }
    }

    private const string ScopeFieldName = "scope";
    private const string MemberFieldName = "member";
    private const string PropertyFieldName = "property";
    private const string AttributeFieldName = "attribute";
    private const string ObjectCreationExprKind = "object_creation_expression";
    private const char DotChar = '.';

    internal static ReadOnlySpan<char> GetCallReceiverSpan(Node callNode)
    {
        if (callNode is CallExprSyntax { Receiver: { } recv })
        {
            return recv.AsSpan();
        }

        var objNode = callNode.GetField(SyntaxFields.Object)
                      ?? callNode.GetField(SyntaxFields.Operand)
                      ?? callNode.GetField(ScopeFieldName);
        if (!objNode.IsValid())
        {
            var funcNode = callNode.GetField(SyntaxFields.Function) ?? (callNode.Children.Count > 0 ? callNode.Children[0] : null);
            if (funcNode.IsValid())
            {
                objNode = funcNode.GetField(SyntaxFields.Expression)
                          ?? funcNode.GetField(SyntaxFields.Object)
                          ?? funcNode.GetField(SyntaxFields.Operand);
                if (!objNode.IsValid())
                {
                    var span = funcNode.AsSpan();
                    var dot = span.LastIndexOf(DotChar);
                    if (dot > 0) return span[..dot].Trim();
                }
            }
        }
        return objNode.IsValid() ? objNode.AsSpan() : ReadOnlySpan<char>.Empty;
    }

    internal static string GetCallReceiverText(Node callNode) => GetCallReceiverSpan(callNode).ToString();

    private static string? CleanPublishTargetArg(Node argNode)
    {
        if (argNode is ArgumentSyntax { Value: { } argVal })
        {
            argNode = argVal;
        }
        else if (argNode.Type == "argument" && argNode.Children.Count > 0)
        {
            argNode = argNode.Children[^1];
        }

        if (IsStringLiteral(argNode))
        {
            return argNode.AsSpan().Trim("\"'`").ToString();
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
            if (ConstantRegistry.TryResolve(null, text, out var directResolved) && !string.IsNullOrEmpty(directResolved))
            {
                return directResolved;
            }
            if (text.Contains('.'))
            {
                var lastPart = text[(text.LastIndexOf('.') + 1)..];
                if (ConstantRegistry.TryResolve(null, lastPart, out var lastResolved) && !string.IsNullOrEmpty(lastResolved))
                {
                    return lastResolved;
                }
                return lastPart;
            }
            return text;
        }

        if (argNode is CallExprSyntax or { Type: "method_invocation" or "call_expression" })
        {
            var receiver = argNode.GetField(SyntaxFields.Object)
                           ?? argNode.Children.FirstOrDefault(c => c is MemberAccessExprSyntax or IdentifierExprSyntax or CallExprSyntax || c.Type is "field_access" or "identifier" or "method_invocation");
            if (receiver.IsValid())
            {
                var receiverText = receiver.Text.Trim('"', '\'');
                if (ConstantRegistry.TryResolve(null, receiverText, out var resolved) && !string.IsNullOrEmpty(resolved))
                {
                    return resolved;
                }
                if (receiverText.Contains('.'))
                {
                    var lastPart = receiverText[(receiverText.LastIndexOf('.') + 1)..];
                    if (ConstantRegistry.TryResolve(null, lastPart, out var memResolved) && !string.IsNullOrEmpty(memResolved))
                    {
                        return memResolved;
                    }
                    return lastPart;
                }
                return receiverText;
            }
        }

        return null;
    }

    #endregion

    #region Shared AST Utility Helpers

    internal static string? GetShortAnnotationName(Node annot)
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

    internal static ReadOnlySpan<char> GetCallMethodSpan(Node callNode)
    {
        if (callNode is CallExprSyntax { MethodNameSlice.Length: > 0 } callExpr)
        {
            return DeclarativeUniversalRuleEvaluator.StripGenericTypeSuffixSpan(callExpr.MethodNameSpan);
        }

        var nameNode = callNode.GetField(SyntaxFields.Name)
                       ?? callNode.GetField(SyntaxFields.Field)
                       ?? callNode.GetField(MemberFieldName)
                       ?? callNode.GetField(SyntaxFields.Constructor)
                       ?? callNode.GetField(SyntaxFields.Type);
        if (nameNode.IsValid()) return DeclarativeUniversalRuleEvaluator.ExtractLeafMethodSpan(nameNode.AsSpan());

        var funcNode = callNode.GetField(SyntaxFields.Function);
        if (funcNode.IsValid())
        {
            var field = funcNode.GetField(SyntaxFields.Field)
                        ?? funcNode.GetField(SyntaxFields.Name)
                        ?? funcNode.GetField(PropertyFieldName)
                        ?? funcNode.GetField(AttributeFieldName);
            if (field.IsValid()) return DeclarativeUniversalRuleEvaluator.StripGenericTypeSuffixSpan(field.AsSpan());
            return DeclarativeUniversalRuleEvaluator.ExtractLeafMethodSpan(funcNode.AsSpan());
        }

        return ReadOnlySpan<char>.Empty;
    }

    internal static string? GetCallMethodName(Node callNode)
    {
        var span = GetCallMethodSpan(callNode);
        return span.IsEmpty ? null : span.ToString();
    }

    internal static Node? FindArgumentsNode(Node callNode)
    {
        return callNode.GetField(SyntaxFields.Arguments)
               ?? callNode.FindChildOfType("argument_list")
               ?? callNode.FindChildOfType("arguments")
               ?? callNode.Children.FirstOrDefault(c => c.Type.Contains("argument", StringComparison.Ordinal));
    }

    internal static bool IsStringLiteral(Node node)
    {
        return node.IsValid() &&
               (node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.RawString } ||
                node.IsAny("string", "encapsed_string") ||
                node.Type.Contains("string_literal", StringComparison.Ordinal));
    }

    internal static List<Node> GetNodeAnnotations(Node declNode, FrozenSet<string> annotationNodeTypes)
    {
        var result = new List<Node>();
        foreach (var child in declNode.Children)
        {
            if (!child.IsValid()) continue;
            if (child is AnnotationSyntax || annotationNodeTypes.Contains(child.Type))
            {
                result.Add(child);
            }
            else if (child is SyntaxList { Kind: SyntaxListKind.Attributes } || child.Type is "modifiers" or "attribute_list" or "decorator_list")
            {
                foreach (var modChild in child.Children)
                {
                    if (modChild.IsValid() && (modChild is AnnotationSyntax || annotationNodeTypes.Contains(modChild.Type)))
                    {
                        result.Add(modChild);
                    }
                }
            }
        }
        return result;
    }

    internal static string? ExtractNamedOrFirstStringFromAnnotation(Node annotNode, FrozenSet<string> acceptedKeys)
    {
        foreach (var child in annotNode.Children)
        {
            if (child is KeyValueExprSyntax || child.Type == "element_value_pair")
            {
                var key = child.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                if (key.IsValid() && acceptedKeys.Contains(key.Text))
                {
                    var val = child.Children.FirstOrDefault(IsStringLiteral);
                    if (val.IsValid()) return val.Text.Trim('"', '\'');
                }
            }
            else if (child is SyntaxList { Kind: SyntaxListKind.Arguments } || child.Type is "annotation_argument_list" or "arguments")
            {
                foreach (var argChild in child.Children)
                {
                    if (argChild is KeyValueExprSyntax or ArgumentSyntax || argChild.Type is "element_value_pair" or "argument")
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

    private static string? ExtractHttpMethodFromAnnotation(Node annotNode)
    {
        foreach (var child in annotNode.Children)
        {
            if (child is SyntaxList { Kind: SyntaxListKind.Arguments } || child.Type is "annotation_argument_list" or "arguments")
            {
                foreach (var argChild in child.Children)
                {
                    if (argChild is KeyValueExprSyntax or ArgumentSyntax || argChild.Type is "element_value_pair" or "argument")
                    {
                        var key = argChild.Children.FirstOrDefault(c => c is IdentifierExprSyntax) ?? argChild.GetField(SyntaxFields.Name);
                        if (key.IsValid() && string.Equals(key.Text, "method", StringComparison.OrdinalIgnoreCase))
                        {
                            var text = argChild.Text;
                            if (text.Contains("GET", StringComparison.OrdinalIgnoreCase)) return "GET";
                            if (text.Contains("POST", StringComparison.OrdinalIgnoreCase)) return "POST";
                            if (text.Contains("PUT", StringComparison.OrdinalIgnoreCase)) return "PUT";
                            if (text.Contains("DELETE", StringComparison.OrdinalIgnoreCase)) return "DELETE";
                            if (text.Contains("PATCH", StringComparison.OrdinalIgnoreCase)) return "PATCH";
                            if (text.Contains("HEAD", StringComparison.OrdinalIgnoreCase)) return "HEAD";
                            if (text.Contains("OPTIONS", StringComparison.OrdinalIgnoreCase)) return "OPTIONS";
                        }
                    }
                }
            }
            else if (child is KeyValueExprSyntax || child.Type == "element_value_pair")
            {
                var key = child.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                if (key.IsValid() && string.Equals(key.Text, "method", StringComparison.OrdinalIgnoreCase))
                {
                    var text = child.Text;
                    if (text.Contains("GET", StringComparison.OrdinalIgnoreCase)) return "GET";
                    if (text.Contains("POST", StringComparison.OrdinalIgnoreCase)) return "POST";
                    if (text.Contains("PUT", StringComparison.OrdinalIgnoreCase)) return "PUT";
                    if (text.Contains("DELETE", StringComparison.OrdinalIgnoreCase)) return "DELETE";
                    if (text.Contains("PATCH", StringComparison.OrdinalIgnoreCase)) return "PATCH";
                    if (text.Contains("HEAD", StringComparison.OrdinalIgnoreCase)) return "HEAD";
                    if (text.Contains("OPTIONS", StringComparison.OrdinalIgnoreCase)) return "OPTIONS";
                }
            }
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

    internal static void CollectNodesByType(Node node, string nodeType, List<Node> result)
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

    internal static string? FindEnclosingMethodName(Node annotNode)
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

    internal static string? ExtractFirstParameterType(Node methodNode)
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
