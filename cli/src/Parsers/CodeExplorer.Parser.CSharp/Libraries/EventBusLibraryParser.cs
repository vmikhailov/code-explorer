using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class EventBusLibraryParser : ISemanticExtension
{
    public string Name => "EventBus";
    public string Id => "eventbus";
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["*EventBus*", "EventBus", "EventBus.*", "RabbitMQ.Client", "RabbitMQ.*"];
    public bool IsBuiltIn => true;
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;

    private static readonly HashSet<string> HandlerInterfaces =
    [
        "IIntegrationEventHandler",
        "IEventHandler"
    ];

    private static readonly HashSet<string> PublishMethodNames =
    [
        "Publish",
        "PublishAsync",
        "AddAndSaveEventAsync",
        "SaveEventAndCatalogContextChangesAsync",
        "PublishThroughEventBusAsync",
        "PublishEventAsync"
    ];

    private static readonly HashSet<string> IgnoredEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "IntegrationEvent",
        "Event",
        "IIntegrationEvent",
        "IEvent",
        "object",
        "dynamic",
        "var",
        "string",
        "Task",
        "ValueTask",
        "void",
        "Boolean",
        "bool",
        "Int32",
        "int",
        "Int64",
        "long",
        "Guid",
        "DateTime",
        "CancellationToken"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHandlerClass(node) || IsHandlerMethod(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsHandlerClass(node))
        {
            var msg = ExtractHandlerMessageType(node);
            return !string.IsNullOrEmpty(msg) ? $"Consumer:{msg}" : "Consumer";
        }

        if (IsHandlerMethod(node))
        {
            var msg = ExtractHandlerMethodMessageType(node);
            return !string.IsNullOrEmpty(msg) ? $"Consumer:{msg}" : "Consumer";
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Ingress: Handler classes subscribe to message types
        if (IsHandlerClass(node))
        {
            var msgTypes = ExtractAllHandlerMessageTypes(node);
            foreach (var msg in msgTypes)
            {
                references.Add(new Reference(scopeSymbolId, "eventbus:" + msg, OntologyConstants.Relationships.SubscribesTo));
            }
        }

        // 2. Ingress & Egress in invocations
        if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            // Ingress via AddSubscription<TEvent, THandler>()
            if (TryExtractAddSubscription(node, out var subMsg))
            {
                references.Add(new Reference(scopeSymbolId, "eventbus:" + subMsg, OntologyConstants.Relationships.SubscribesTo));
            }

            // Egress via PublishAsync / Publish / AddAndSaveEventAsync / SaveEventAndCatalogContextChangesAsync
            var egressMsgs = ExtractEgressMessages(node);
            foreach (var egressMsg in egressMsgs)
            {
                references.Add(new Reference(scopeSymbolId, "eventbus:" + egressMsg, OntologyConstants.Relationships.PublishesTo));
            }
        }
    }

    private static bool IsHandlerClass(Node node)
    {
        if (!node.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            return false;

        var baseList = node.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (!baseList.IsValid()) return false;

        return ExtractAllHandlerMessageTypes(node).Count > 0;
    }

    private static bool IsHandlerMethod(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.MethodDeclaration)) return false;

        // Skip methods declared in interfaces
        var parent = node.Parent;
        while (parent.IsValid())
        {
            if (parent.Is(TreeSitterSyntax.CSharp.InterfaceDeclaration)) return false;
            if (parent.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration)) break;
            parent = parent.Parent;
        }

        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return false;

        var name = nameNode.Text;
        if (name is not ("Handle" or "HandleAsync" or "Consume")) return false;

        return !string.IsNullOrEmpty(ExtractHandlerMethodMessageType(node));
    }

    private static string? ExtractHandlerMessageType(Node classNode)
    {
        var all = ExtractAllHandlerMessageTypes(classNode);
        return all.Count > 0 ? all[0] : null;
    }

    private static List<string> ExtractAllHandlerMessageTypes(Node classNode)
    {
        var result = new List<string>();
        var baseList = classNode.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (!baseList.IsValid()) return result;

        foreach (var child in baseList.Children)
        {
            var genName = child.Is(TreeSitterSyntax.CSharp.GenericName)
                ? child
                : child.FindDescendantOfType(TreeSitterSyntax.CSharp.GenericName);

            if (genName.IsValid())
            {
                var idNode = genName.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                if (idNode.IsValid() && HandlerInterfaces.Contains(idNode.Text))
                {
                    var typeArgList = genName.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                    if (typeArgList.IsValid())
                    {
                        var typeNode = typeArgList.Children.FirstOrDefault(c =>
                            c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.QualifiedName));
                        if (typeNode.IsValid() && !string.IsNullOrEmpty(typeNode.Text))
                        {
                            var clean = CleanTypeName(typeNode.Text);
                            if (IsValidConcreteEventType(clean))
                            {
                                result.Add(clean);
                            }
                        }
                    }
                }
            }
        }

        return result;
    }

    private static string? ExtractHandlerMethodMessageType(Node methodNode)
    {
        var paramList = methodNode.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
        if (!paramList.IsValid()) return null;

        foreach (var param in paramList.FindChildrenOfType(TreeSitterSyntax.CSharp.Parameter))
        {
            var typeNode = param.GetField(TreeSitterSyntax.Fields.Type)
                ?? param.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.QualifiedName));
            if (typeNode.IsValid())
            {
                var clean = CleanTypeName(typeNode.Text);
                if (IsValidConcreteEventType(clean))
                {
                    return clean;
                }
            }
        }

        return null;
    }

    private static bool TryExtractAddSubscription(Node invocationNode, out string messageType)
    {
        messageType = "";
        var func = invocationNode.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            return false;

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return false;

        if (nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            var idChild = nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier);
            if (idChild.IsValid() && idChild.Text == "AddSubscription")
            {
                var typeArgs = nameNode.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                if (typeArgs.IsValid())
                {
                    var firstType = typeArgs.Children.FirstOrDefault(c =>
                        c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.QualifiedName));
                    if (firstType.IsValid())
                    {
                        var clean = CleanTypeName(firstType.Text);
                        if (IsValidConcreteEventType(clean))
                        {
                            messageType = clean;
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private static HashSet<string> ExtractEgressMessages(Node invocationNode)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);
        var func = invocationNode.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            return results;

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return results;

        string methodName;
        if (nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            methodName = nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text ?? "";
        }
        else
        {
            methodName = nameNode.Text;
        }

        if (!PublishMethodNames.Contains(methodName)) return results;

        // Case A: Generic method call: PublishAsync<OrderStartedIntegrationEvent>(...)
        if (nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            var typeArgs = nameNode.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
            if (typeArgs.IsValid())
            {
                var firstType = typeArgs.Children.FirstOrDefault(c =>
                    c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.QualifiedName));
                if (firstType.IsValid())
                {
                    var clean = CleanTypeName(firstType.Text);
                    if (IsValidConcreteEventType(clean))
                    {
                        results.Add(clean);
                    }
                }
            }
        }

        if (results.Count > 0) return results;

        // Inspect argument list
        var argList = invocationNode.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (!argList.IsValid()) return results;

        var args = argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument).ToList();
        if (args.Count == 0) return results;

        var firstArg = args[0];

        // Case B: Inline ObjectCreationExpression inside the argument:
        // e.g. PublishAsync(new OrderStartedIntegrationEvent(...)) or (IntegrationEvent)new OrderStartedIntegrationEvent(...)
        foreach (var objCreation in FindSelfAndDescendantsOfType(firstArg, TreeSitterSyntax.CSharp.ObjectCreationExpression))
        {
            var typeNode = objCreation.GetField(TreeSitterSyntax.Fields.Type)
                ?? objCreation.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.QualifiedName));
            if (typeNode.IsValid())
            {
                var typeName = CleanTypeName(typeNode.Text);
                if (IsValidConcreteEventType(typeName))
                {
                    results.Add(typeName);
                }
            }
        }

        if (results.Count > 0) return results;

        // Case C: The argument is an identifier / variable:
        // Walk the enclosing declaration and find its variable declarations or assignments.
        var idNode = firstArg.FindDescendantOfType(TreeSitterSyntax.Common.Identifier)
            ?? (firstArg.Children.Count > 0 && firstArg.Children[0].Is(TreeSitterSyntax.Common.Identifier) ? firstArg.Children[0] : null);

        if (idNode.IsValid())
        {
            var varName = idNode.Text;
            if (!string.IsNullOrEmpty(varName))
            {
                ResolveVariableEventTypes(invocationNode, varName, results);
            }
        }

        return results;
    }

    private static void ResolveVariableEventTypes(Node callNode, string varName, HashSet<string> results)
    {
        var scope = FindEnclosingDeclaration(callNode);
        if (!scope.IsValid()) return;

        // 1. VariableDeclarator matches
        foreach (var decl in scope.FindDescendantsOfType(TreeSitterSyntax.CSharp.VariableDeclarator))
        {
            var nameChild = decl.GetField(TreeSitterSyntax.Fields.Name)
                ?? decl.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Common.Identifier));

            if (nameChild.IsValid() && nameChild.Text == varName)
            {
                // Explicit type annotation on variable_declaration:
                var varDecl = decl.Parent;
                if (varDecl.IsValid() && varDecl.Is(TreeSitterSyntax.CSharp.VariableDeclaration))
                {
                    var typeChild = varDecl.GetField(TreeSitterSyntax.Fields.Type)
                        ?? varDecl.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.QualifiedName));
                    if (typeChild.IsValid())
                    {
                        var declaredType = CleanTypeName(typeChild.Text);
                        if (IsValidConcreteEventType(declaredType))
                        {
                            results.Add(declaredType);
                        }
                    }
                }

                // All object creations within this declarator (initializers, ternaries, etc.)
                foreach (var objCreation in FindSelfAndDescendantsOfType(decl, TreeSitterSyntax.CSharp.ObjectCreationExpression))
                {
                    var typeNode = objCreation.GetField(TreeSitterSyntax.Fields.Type)
                        ?? objCreation.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.QualifiedName));
                    if (typeNode.IsValid())
                    {
                        var typeName = CleanTypeName(typeNode.Text);
                        if (IsValidConcreteEventType(typeName))
                        {
                            results.Add(typeName);
                        }
                    }
                }
            }
        }

        // 2. AssignmentExpression matches: varName = new SomeEvent(...);
        foreach (var assign in scope.FindDescendantsOfType(TreeSitterSyntax.Common.AssignmentExpression))
        {
            var left = assign.GetField(TreeSitterSyntax.Fields.Left)
                ?? assign.Children.FirstOrDefault(c => c.IsValid());

            if (left.IsValid() && left.Text == varName)
            {
                var right = assign.GetField(TreeSitterSyntax.Fields.Right)
                    ?? assign.Children.LastOrDefault(c => c.IsValid());

                if (right.IsValid())
                {
                    foreach (var objCreation in FindSelfAndDescendantsOfType(right, TreeSitterSyntax.CSharp.ObjectCreationExpression))
                    {
                        var typeNode = objCreation.GetField(TreeSitterSyntax.Fields.Type)
                            ?? objCreation.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.QualifiedName));
                        if (typeNode.IsValid())
                        {
                            var typeName = CleanTypeName(typeNode.Text);
                            if (IsValidConcreteEventType(typeName))
                            {
                                results.Add(typeName);
                            }
                        }
                    }
                }
            }
        }
    }

    private static Node? FindEnclosingDeclaration(Node node)
    {
        var curr = node.Parent;
        Node? topBlock = null;
        while (curr.IsValid())
        {
            if (curr.IsAny(
                TreeSitterSyntax.CSharp.MethodDeclaration,
                TreeSitterSyntax.CSharp.ConstructorDeclaration,
                TreeSitterSyntax.CSharp.LocalFunctionStatement,
                TreeSitterSyntax.CSharp.PropertyDeclaration,
                TreeSitterSyntax.CSharp.CompilationUnit))
            {
                return curr;
            }
            if (curr.Is(TreeSitterSyntax.CSharp.Block) && topBlock == null)
            {
                topBlock = curr;
            }
            curr = curr.Parent;
        }
        return topBlock;
    }

    private static IEnumerable<Node> FindSelfAndDescendantsOfType(Node node, string type)
    {
        if (node.Is(type)) yield return node;
        foreach (var d in node.FindDescendantsOfType(type)) yield return d;
    }

    private static bool IsValidConcreteEventType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return false;
        var clean = CleanTypeName(typeName);
        if (IgnoredEventTypes.Contains(clean)) return false;

        // Reject generic type parameter placeholders like TIntegrationEvent, TEvent, TMessage, T
        if (clean.StartsWith("T") && clean.Length > 1 && char.IsUpper(clean[1]) &&
            (clean.EndsWith("Event", StringComparison.Ordinal) || clean.EndsWith("Message", StringComparison.Ordinal)))
        {
            return false;
        }

        return clean.Length >= 2 && (clean.EndsWith("IntegrationEvent", StringComparison.Ordinal) || clean.EndsWith("Event", StringComparison.Ordinal));
    }

    private static string CleanTypeName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return string.Empty;
        var dot = typeName.LastIndexOf('.');
        var clean = dot >= 0 ? typeName[(dot + 1)..] : typeName;
        clean = clean.TrimStart('@').TrimEnd('?');
        return clean;
    }
}
