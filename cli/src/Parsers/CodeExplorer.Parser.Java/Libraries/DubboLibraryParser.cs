using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Java.Libraries;

public class DubboLibraryParser : DeclarativeSemanticExtension
{
    private static class Constants
    {
        public const string ExtensionId = "dubbo";
        public const string ExtensionName = "Apache Dubbo";
        public const string ProtocolDubbo = "dubbo";
        public const string ProtocolTri = "tri";
        public const string ProtocolGrpc = "gRPC";
        public const string DubboService = "DubboService";
        public const string Service = "Service";
        public const string DubboReference = "DubboReference";
        public const string Reference = "Reference";
        public const string ImplBaseSuffix = "ImplBase";
        public const string ImplSuffix = "Impl";
        public const string InterfaceClassAttr = "interfaceClass";
        public const string InterfaceNameAttr = "interfaceName";
        public const string ProtocolAttr = "protocol";
        public const string Anonymous = "anonymous";
        public const string RpcPrefix = "RPC:/";
        public const string DubboScheme = "dubbo://";
        public const string TriScheme = "tri://";
        public const string StreamObserver = "StreamObserver";
        public const string TripleKeyword = "triple";
    }

    private static readonly HashSet<string> IgnoredInterfaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "Serializable", "Comparable", "Cloneable", "AutoCloseable", "Closeable",
        "InitializingBean", "DisposableBean", "ApplicationContextAware", "BeanPostProcessor",
        "BeanFactoryAware", "Ordered", "PriorityOrdered"
    };

    private static readonly HashSet<string> IgnoredMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "toString", "equals", "hashCode", "clone", "finalize", "getClass", "wait", "notify", "notifyAll", "main"
    };

    public DubboLibraryParser()
    {
        Id = Constants.ExtensionId;
        Name = Constants.ExtensionName;
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "org.apache.dubbo",
            "org.apache.dubbo.*",
            "com.alibaba.dubbo",
            "com.alibaba.dubbo.*"
        ];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet(
                [
                    TreeSitterSyntax.Java.MethodDeclaration,
                    TreeSitterSyntax.Java.FieldDeclaration,
                    TreeSitterSyntax.Java.MethodInvocation
                ]),
                MapNodeType = MapNodeTypeCore,
                ExtractIdentifier = (node, ctx, _, _) => ExtractIdentifierCore(node),
                CollectReferences = CollectReferencesCore,
                EnrichSymbol = EnrichSymbolCore
            }
        ];
    }

    private static string? MapNodeTypeCore(Node node, ParsingContext ctx)
    {
        if (IsDubboServiceMethod(node)) return OntologyConstants.NodeLabels.EntryPoint;
        if (IsDubboReferenceField(node)) return OntologyConstants.NodeLabels.ExternalService;
        if (IsDubboMethodInvocation(node)) return OntologyConstants.NodeLabels.ExternalService;
        return null;
    }

    private static string? ExtractIdentifierCore(Node node)
    {
        if (IsDubboServiceMethod(node))
        {
            var methodName = GetMethodName(node);
            var interfaceName = GetServiceInterfaceName(node);
            return string.IsNullOrEmpty(interfaceName)
                ? $"{Constants.RpcPrefix}{methodName}"
                : $"{Constants.RpcPrefix}{interfaceName}/{methodName}";
        }

        if (IsDubboReferenceField(node))
        {
            var interfaceName = GetReferenceInterfaceName(node);
            var proto = GetReferenceProtocol(node);
            var scheme = proto == Constants.ProtocolTri ? Constants.TriScheme : Constants.DubboScheme;
            return $"{scheme}{interfaceName}";
        }

        if (IsDubboMethodInvocation(node))
        {
            var methodName = GetInvocationMethodName(node);
            var (interfaceName, proto) = GetInvocationTarget(node);
            var scheme = proto == Constants.ProtocolTri ? Constants.TriScheme : Constants.DubboScheme;
            return $"{scheme}{interfaceName}/{methodName}";
        }

        return null;
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsDubboReferenceField(node) || IsDubboMethodInvocation(node))
        {
            var target = ExtractIdentifierCore(node);
            if (!string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
            }
        }
    }

    private static void EnrichSymbolCore(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsDubboServiceMethod(node))
        {
            var classNode = GetParentClassDeclaration(node);
            var isTri = classNode.IsValid() && GetServiceProtocol(classNode) == Constants.ProtocolTri;
            symbol.Protocol = isTri ? Constants.ProtocolTri : Constants.ProtocolDubbo;
            symbol.OperationType = CodeExplorer.Cypher.Common.OperationType.Unary;

            var paramList = node.GetField(TreeSitterSyntax.Fields.Parameters);
            if (paramList.IsValid())
            {
                foreach (var param in paramList.Children)
                {
                    if (!param.Is(TreeSitterSyntax.Java.FormalParameter)) continue;
                    var pType = param.GetField(TreeSitterSyntax.Fields.Type);
                    if (pType.IsValid() && symbol.RequestType == null)
                    {
                        var pText = CleanTypeName(pType.Text);
                        if (!IsJavaPrimitiveOrSystemType(pText))
                        {
                            symbol.RequestType = pText;
                        }
                    }
                }
            }

            var typeNode = node.GetField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid() && symbol.ResponseType == null)
            {
                var retText = CleanTypeName(typeNode.Text);
                if (!string.IsNullOrEmpty(retText) && retText != "void")
                {
                    symbol.ResponseType = retText;
                }
            }
        }
        else if (IsDubboReferenceField(node))
        {
            symbol.Protocol = GetReferenceProtocol(node);
        }
        else if (IsDubboMethodInvocation(node))
        {
            var (_, proto) = GetInvocationTarget(node);
            symbol.Protocol = proto;
        }
    }

    private static bool IsDubboServiceMethod(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Java.MethodDeclaration)) return false;

        var classNode = GetParentClassDeclaration(node);
        if (!classNode.IsValid()) return false;

        if (!IsDubboServiceClass(classNode)) return false;

        // Verify method is not private, protected, static or object method
        var modifiers = node.FindChildOfType(TreeSitterSyntax.Java.Modifiers);
        if (modifiers.IsValid())
        {
            var modText = modifiers.Text;
            if (modText.Contains("private") || modText.Contains("protected") || modText.Contains("static"))
            {
                return false;
            }
        }

        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        var methodName = nameNode.IsValid() ? nameNode.Text : null;
        if (string.IsNullOrEmpty(methodName) || IgnoredMethods.Contains(methodName))
        {
            return false;
        }

        return true;
    }

    private static bool IsDubboServiceClass(Node classNode)
    {
        var modifiers = classNode.FindChildOfType(TreeSitterSyntax.Java.Modifiers);
        if (!modifiers.IsValid()) return false;

        foreach (var child in modifiers.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
            {
                var text = child.Text;
                if (text.Contains(Constants.DubboService))
                {
                    return true;
                }

                if (text.Contains(Constants.Service))
                {
                    // Check if it has Dubbo-specific attributes or imports
                    if (text.Contains(Constants.InterfaceClassAttr) ||
                        text.Contains(Constants.InterfaceNameAttr) ||
                        text.Contains(Constants.ProtocolAttr) ||
                        text.Contains("version") ||
                        text.Contains("group") ||
                        text.Contains("timeout"))
                    {
                        return true;
                    }

                    // Check imports in root file node
                    if (HasDubboImport(classNode))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasDubboImport(Node node)
    {
        var root = node;
        while (root.Parent.IsValid()) root = root.Parent;

        foreach (var child in root.Children)
        {
            if (child.Is(TreeSitterSyntax.Java.ImportDeclaration) &&
                (child.Text.Contains("org.apache.dubbo") || child.Text.Contains("com.alibaba.dubbo")))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDubboReferenceField(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Java.FieldDeclaration)) return false;

        var modifiers = node.FindChildOfType(TreeSitterSyntax.Java.Modifiers);
        if (!modifiers.IsValid()) return false;

        foreach (var child in modifiers.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
            {
                var text = child.Text;
                if (text.Contains(Constants.DubboReference))
                {
                    return true;
                }

                if (text.Contains(Constants.Reference))
                {
                    if (text.Contains(Constants.InterfaceClassAttr) ||
                        text.Contains(Constants.ProtocolAttr) ||
                        text.Contains("version") ||
                        text.Contains("group") ||
                        text.Contains("check") ||
                        text.Contains("timeout") ||
                        HasDubboImport(node))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsDubboMethodInvocation(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Java.MethodInvocation)) return false;

        var objNode = node.GetField(TreeSitterSyntax.Fields.Object);
        if (!objNode.IsValid()) return false;

        var receiverName = objNode.Text;
        if (string.IsNullOrEmpty(receiverName) || receiverName.Contains('.')) return false;

        var classNode = GetParentClassDeclaration(node);
        if (!classNode.IsValid()) return false;

        var field = FindClassFieldByName(classNode, receiverName);
        return field.IsValid() && IsDubboReferenceField(field);
    }

    private static Node? FindClassFieldByName(Node classNode, string variableName)
    {
        const string classBodyType = "class_body";
        var body = classNode.FindChildOfType(classBodyType);
        if (!body.IsValid()) return null;

        foreach (var child in body.Children)
        {
            if (child.Is(TreeSitterSyntax.Java.FieldDeclaration))
            {
                foreach (var decl in child.Children)
                {
                    if (decl.Is(TreeSitterSyntax.Java.VariableDeclarator))
                    {
                        var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name);
                        if (nameNode.IsValid() && nameNode.Text == variableName)
                        {
                            return child;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string GetMethodName(Node node)
    {
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        return nameNode.IsValid() ? nameNode.Text : Constants.Anonymous;
    }

    private static string GetInvocationMethodName(Node node)
    {
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        return nameNode.IsValid() ? nameNode.Text : Constants.Anonymous;
    }

    private static (string InterfaceName, string Protocol) GetInvocationTarget(Node node)
    {
        var objNode = node.GetField(TreeSitterSyntax.Fields.Object);
        var receiverName = objNode.IsValid() ? objNode.Text : "";

        var classNode = GetParentClassDeclaration(node);
        if (classNode.IsValid())
        {
            var field = FindClassFieldByName(classNode, receiverName);
            if (field.IsValid())
            {
                var iface = GetReferenceInterfaceName(field);
                var proto = GetReferenceProtocol(field);
                return (iface, proto);
            }
        }

        return (receiverName, Constants.ProtocolDubbo);
    }

    private static string GetServiceInterfaceName(Node methodNode)
    {
        var classNode = GetParentClassDeclaration(methodNode);
        if (!classNode.IsValid()) return Constants.Anonymous;

        // 1. Explicit interfaceClass or interfaceName in annotation
        var annotIface = ExtractInterfaceFromAnnotation(classNode);
        if (!string.IsNullOrEmpty(annotIface))
        {
            return annotIface;
        }

        // 2. Declared interfaces (super_interfaces)
        var ifaces = classNode.FindChildOfType(TreeSitterSyntax.Java.SuperInterfaces);
        if (ifaces.IsValid())
        {
            foreach (var child in ifaces.Children)
            {
                var candidate = CleanTypeName(child.Text);
                if (!string.IsNullOrEmpty(candidate) &&
                    candidate != "implements" &&
                    candidate != "," &&
                    !IgnoredInterfaces.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        // 3. Superclass ending with ImplBase (e.g. GreeterImplBase -> Greeter)
        var superclass = classNode.FindChildOfType(TreeSitterSyntax.Java.Superclass);
        if (superclass.IsValid())
        {
            var sName = CleanTypeName(superclass.Text);
            if (sName.StartsWith("extends")) sName = sName["extends".Length..].Trim();
            if (sName.EndsWith(Constants.ImplBaseSuffix, StringComparison.Ordinal) && sName.Length > Constants.ImplBaseSuffix.Length)
            {
                return sName[..^Constants.ImplBaseSuffix.Length];
            }
        }

        // 4. Class name stripped of 'Impl'
        var classNameNode = classNode.FindChildOfType(TreeSitterSyntax.Java.Identifier);
        var className = classNameNode.IsValid() ? classNameNode.Text : Constants.Anonymous;
        if (className.EndsWith(Constants.ImplSuffix, StringComparison.Ordinal) && className.Length > Constants.ImplSuffix.Length)
        {
            return className[..^Constants.ImplSuffix.Length];
        }

        return className;
    }

    private static string GetReferenceInterfaceName(Node fieldNode)
    {
        var annotIface = ExtractInterfaceFromAnnotation(fieldNode);
        if (!string.IsNullOrEmpty(annotIface))
        {
            return annotIface;
        }

        var typeNode = fieldNode.GetField(TreeSitterSyntax.Fields.Type);
        if (typeNode.IsValid())
        {
            return CleanTypeName(typeNode.Text);
        }

        return Constants.Anonymous;
    }

    private static string ExtractInterfaceFromAnnotation(Node annotatedNode)
    {
        var modifiers = annotatedNode.FindChildOfType(TreeSitterSyntax.Java.Modifiers);
        if (!modifiers.IsValid()) return "";

        foreach (var annot in modifiers.Children)
        {
            if (annot.IsAny(TreeSitterSyntax.Java.Annotation, TreeSitterSyntax.Java.MarkerAnnotation))
            {
                var text = annot.Text;
                var match = Regex.Match(text, @"(?:interfaceClass\s*=\s*|interfaceName\s*=\s*""?)([A-Za-z0-9_$.]+)");
                if (match.Success)
                {
                    var full = match.Groups[1].Value.Replace(".class", "").Trim();
                    return CleanTypeName(full);
                }
            }
        }

        return "";
    }

    private static string GetReferenceProtocol(Node fieldNode)
    {
        var modifiers = fieldNode.FindChildOfType(TreeSitterSyntax.Java.Modifiers);
        if (modifiers.IsValid())
        {
            var text = modifiers.Text;
            if (text.Contains("protocol = \"tri\"") || text.Contains("protocol=\"tri\"") || text.Contains(Constants.TripleKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ProtocolTri;
            }
        }

        var typeNode = fieldNode.GetField(TreeSitterSyntax.Fields.Type);
        if (typeNode.IsValid() && typeNode.Text.Contains(Constants.TripleKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return Constants.ProtocolTri;
        }

        return Constants.ProtocolDubbo;
    }

    private static string GetServiceProtocol(Node classNode)
    {
        var modifiers = classNode.FindChildOfType(TreeSitterSyntax.Java.Modifiers);
        if (modifiers.IsValid())
        {
            var text = modifiers.Text;
            if (text.Contains("protocol = \"tri\"") || text.Contains("protocol=\"tri\"") || text.Contains(Constants.TripleKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return Constants.ProtocolTri;
            }
        }

        var superclass = classNode.FindChildOfType(TreeSitterSyntax.Java.Superclass);
        if (superclass.IsValid() && (superclass.Text.Contains(Constants.TripleKeyword, StringComparison.OrdinalIgnoreCase) || superclass.Text.EndsWith(Constants.ImplBaseSuffix)))
        {
            return Constants.ProtocolTri;
        }

        return Constants.ProtocolDubbo;
    }

    private static Node? GetParentClassDeclaration(Node node)
    {
        var parent = node.Parent;
        while (parent.IsValid())
        {
            if (parent.Is(TreeSitterSyntax.Java.ClassDeclaration) || parent.Is(TreeSitterSyntax.Java.RecordDeclaration))
            {
                return parent;
            }
            parent = parent.Parent;
        }
        return null;
    }

    private static string CleanTypeName(string rawType)
    {
        if (string.IsNullOrEmpty(rawType)) return "";
        var t = rawType.Trim();
        var angleIdx = t.IndexOf('<');
        if (angleIdx > 0) t = t[..angleIdx].Trim();
        var dotIdx = t.LastIndexOf('.');
        if (dotIdx >= 0 && dotIdx < t.Length - 1) t = t[(dotIdx + 1)..].Trim();
        return t;
    }

    private static bool IsJavaPrimitiveOrSystemType(string type)
    {
        return type is "int" or "long" or "String" or "boolean" or "double" or "float" or "byte" or "short" or "char"
            or "Integer" or "Long" or "Boolean" or "Double" or "Float" or "UUID" or "Object" or "void";
    }
}
