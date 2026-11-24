using System.Collections.Frozen;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

internal static class DeclarativeOutboundAndSecurityHelper
{
    private static readonly FrozenSet<string> SecurityAnnotNodeTypes =
        new[] { "annotation", "marker_annotation", "attribute", "attribute_group" }.ToFrozenSet();
    private static readonly FrozenSet<string> RolesValueKeys =
        new[] { "value", "roles" }.ToFrozenSet();
    private static readonly FrozenSet<string> ValueOnlyKeys =
        new[] { "value" }.ToFrozenSet();

    private const string VoidType = "void";
    private const string ParameterSub = "parameter";
    private const string RequestBodyMarker = "@RequestBody";
    private const string ModelAttributeMarker = "@ModelAttribute";
    private const string HasRoleMarker = "hasRole";
    private const string HasAuthorityMarker = "hasAuthority";
    private const string GraphQlProtocol = "GraphQL";
    private const string GrpcProtocol = "gRPC";
    private const string ElementValuePairType = "element_value_pair";
    private const string PropertyElementType = "property_element";
    private const string ClassBodyType = "class_body";
    private const string StatementBlockType = "statement_block";
    private const string CompoundStatementType = "compound_statement";
    private const string PlaceholderPrefix = "${";
    private const string HttpPrefix = "http";
    private const string LbPrefix = "lb:";
    private const string WildcardStr = "*";
    private const string SlashStr = "/";
    private const char OpenAngleChar = '<';
    private const char CloseAngleChar = '>';
    private const char SingleQuoteChar = '\'';
    private const char DoubleQuoteChar = '"';
    private const char SlashChar = '/';
    private const char QuestionChar = '?';
    private const char DollarChar = '$';
    private const char DashChar = '-';
    private const char ColonChar = ':';
    private const char CloseBraceChar = '}';

    internal static void EnrichFromEnclosingDeclarations(Node annotationNode, SyntacticSymbol symbol)
    {
        var targetDecl = annotationNode.Parent;
        while (targetDecl.IsValid())
        {
            if (targetDecl is FunctionDeclSyntax or TypeDeclSyntax)
            {
                EnrichSecurityFromAnnotations(targetDecl, symbol);
                break;
            }
            targetDecl = targetDecl.Parent;
        }

        if (targetDecl is FunctionDeclSyntax)
        {
            ExtractMethodPayloadSchemas(targetDecl, symbol);

            var classDecl = targetDecl.Parent;
            while (classDecl.IsValid())
            {
                if (classDecl is TypeDeclSyntax)
                {
                    EnrichSecurityFromAnnotations(classDecl, symbol);
                    break;
                }
                classDecl = classDecl.Parent;
            }
        }
    }

    private static void ExtractMethodPayloadSchemas(Node methodDecl, SyntacticSymbol symbol)
    {
        var typeNode = methodDecl.GetField(SyntaxFields.Type);
        if (typeNode.IsValid())
        {
            var ret = UnwrapGenericResponseType(typeNode.Text);
            if (!string.IsNullOrEmpty(ret) && ret != VoidType)
            {
                symbol.ResponseType = ret;
            }
        }

        var paramList = methodDecl.GetField(SyntaxFields.Parameters);
        if (paramList.IsValid())
        {
            foreach (var param in paramList.Children)
            {
                if (param is not ParameterDeclSyntax && !param.Type.Contains(ParameterSub, StringComparison.Ordinal)) continue;

                var isRequestBody = param.Text.Contains(RequestBodyMarker, StringComparison.Ordinal) ||
                                    param.Text.Contains(ModelAttributeMarker, StringComparison.Ordinal);
                var pType = param.GetField(SyntaxFields.Type);
                if (pType.IsValid())
                {
                    var pTypeName = UnwrapGenericResponseType(pType.Text);
                    if (isRequestBody || (!IsPrimitiveOrFrameworkParamType(pTypeName) && symbol.RequestType == null))
                    {
                        symbol.RequestType = pTypeName;
                        if (isRequestBody) break;
                    }
                }
            }
        }
    }

    private static string UnwrapGenericResponseType(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "";
        var type = rawType.Trim();
        while (true)
        {
            var genericIdx = type.IndexOf(OpenAngleChar);
            if (genericIdx > 0 && type.EndsWith(CloseAngleChar))
            {
                var outer = type[..genericIdx].Trim();
                if (outer is "ResponseEntity" or "CompletableFuture" or "Mono" or "Flux" or "List" or "Set" or "Collection" or "Optional" or "HttpEntity")
                {
                    type = type[(genericIdx + 1)..^1].Trim();
                    continue;
                }
            }
            break;
        }
        return type;
    }

    private static bool IsPrimitiveOrFrameworkParamType(string type)
    {
        return type is "int" or "long" or "String" or "boolean" or "double" or "float" or "byte" or "short" or "char"
            or "Integer" or "Long" or "Boolean" or "Double" or "Float" or "UUID" or "Date" or "Instant" or "LocalDate"
            or "LocalDateTime" or "HttpServletRequest" or "HttpServletResponse" or "Principal" or "Authentication"
            or "HttpSession" or "Object";
    }

    private static void EnrichSecurityFromAnnotations(Node targetDecl, SyntacticSymbol symbol)
    {
        var annotations = DeclarativeSemanticExtension.GetNodeAnnotations(targetDecl, SecurityAnnotNodeTypes);
        foreach (var annot in annotations)
        {
            var name = DeclarativeSemanticExtension.GetShortAnnotationName(annot);
            if (string.IsNullOrEmpty(name)) continue;

            if (name is "PermitAll" or "AnonymousAllowed" or "AllowAnonymous")
            {
                symbol.IsAnonymous = true;
            }
            else if (name is "RolesAllowed" or "Secured")
            {
                var role = DeclarativeSemanticExtension.ExtractNamedOrFirstStringFromAnnotation(annot, RolesValueKeys);
                if (!string.IsNullOrEmpty(role))
                {
                    symbol.RequiredRoles = string.IsNullOrEmpty(symbol.RequiredRoles) ? role : $"{symbol.RequiredRoles},{role}";
                }
            }
            else if (name is "PreAuthorize" or "PostAuthorize")
            {
                var expr = DeclarativeSemanticExtension.ExtractNamedOrFirstStringFromAnnotation(annot, ValueOnlyKeys);
                if (!string.IsNullOrEmpty(expr))
                {
                    symbol.Policies = string.IsNullOrEmpty(symbol.Policies) ? expr : $"{symbol.Policies},{expr}";
                    if (expr.Contains(HasRoleMarker, StringComparison.OrdinalIgnoreCase) || expr.Contains(HasAuthorityMarker, StringComparison.OrdinalIgnoreCase))
                    {
                        var startQuote = expr.IndexOf(SingleQuoteChar);
                        var endQuote = expr.LastIndexOf(SingleQuoteChar);
                        if (startQuote >= 0 && endQuote > startQuote)
                        {
                            var role = expr[(startQuote + 1)..endQuote];
                            symbol.RequiredRoles = string.IsNullOrEmpty(symbol.RequiredRoles) ? role : $"{symbol.RequiredRoles},{role}";
                        }
                    }
                }
            }
            else if (name is "QueryMapping" or "MutationMapping" or "SubscriptionMapping")
            {
                symbol.Protocol = GraphQlProtocol;
            }
            else if (name is "GrpcService")
            {
                symbol.Protocol = GrpcProtocol;
            }
        }
    }

    internal static bool MatchesOutboundHttp(Node node, OutboundHttpRule rule)
    {
        if (DeclarativeUniversalRuleEvaluator.IsCallNode(node, rule.CallNodeTypes))
        {
            var methodSpan = DeclarativeSemanticExtension.GetCallMethodSpan(node);
            if (!methodSpan.IsEmpty && rule.CallMethodNames.GetAlternateLookup<ReadOnlySpan<char>>().Contains(methodSpan))
            {
                return true;
            }
        }

        if (node is AnnotationSyntax || (node.SyntaxKind == SyntaxKind.Annotation && rule.AnnotationNodeTypes.Contains(node.Type)))
        {
            var annotName = DeclarativeSemanticExtension.GetShortAnnotationName(node);
            if (!string.IsNullOrEmpty(annotName) &&
                rule.ClientAnnotationSuffixes.Any(suffix => annotName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (node is TypeDeclSyntax { Kind: TypeSyntaxKind.Interface } && rule.ClientAnnotationSuffixes.Count > 0)
        {
            var annotations = DeclarativeSemanticExtension.GetNodeAnnotations(node, rule.AnnotationNodeTypes);
            if (annotations.Any(a =>
                {
                    var n = DeclarativeSemanticExtension.GetShortAnnotationName(a);
                    return !string.IsNullOrEmpty(n) &&
                           rule.ClientAnnotationSuffixes.Any(s => n.EndsWith(s, StringComparison.OrdinalIgnoreCase));
                }))
            {
                return true;
            }
        }

        return false;
    }

    internal static string ExtractOutboundHttpTarget(Node node, OutboundHttpRule rule, string? projectName = null)
    {
        var targetNode = node;
        if (node is TypeDeclSyntax { Kind: TypeSyntaxKind.Interface })
        {
            var annotations = DeclarativeSemanticExtension.GetNodeAnnotations(node, rule.AnnotationNodeTypes);
            var clientAnnot = annotations.FirstOrDefault(a =>
            {
                var n = DeclarativeSemanticExtension.GetShortAnnotationName(a);
                return !string.IsNullOrEmpty(n) &&
                       rule.ClientAnnotationSuffixes.Any(s => n.EndsWith(s, StringComparison.OrdinalIgnoreCase));
            });
            if (clientAnnot.IsValid())
            {
                targetNode = clientAnnot;
            }
        }

        if (targetNode is AnnotationSyntax || (targetNode.SyntaxKind == SyntaxKind.Annotation && rule.AnnotationNodeTypes.Contains(targetNode.Type)))
        {
            string? nameVal = null;
            string? pathVal = null;

            var str = targetNode.Children.FirstOrDefault(DeclarativeSemanticExtension.IsStringLiteral);
            if (str.IsValid()) nameVal = str.Text.Trim(DoubleQuoteChar, SingleQuoteChar);

            var pairs = new List<Node>();
            DeclarativeSemanticExtension.CollectNodesByType(targetNode, ElementValuePairType, pairs);
            foreach (var pair in pairs)
            {
                var key = pair.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                var val = pair.Children.FirstOrDefault(DeclarativeSemanticExtension.IsStringLiteral);
                if (key.IsValid() && val.IsValid())
                {
                    var keyText = key.Text;
                    var valText = val.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                    if (keyText is "name" or "value" or "configKey" or "serviceId" or "id")
                    {
                        nameVal = valText;
                    }
                    else if (keyText is "path")
                    {
                        pathVal = valText;
                    }
                    else if (keyText is "url")
                    {
                        var rawUrl = valText;
                        if (rawUrl.StartsWith(PlaceholderPrefix, StringComparison.Ordinal) && rawUrl.EndsWith(CloseBraceChar))
                        {
                            var propKey = rawUrl[2..^1].Trim();
                            if (ConstantRegistry.TryResolve(null, propKey, out var constVal) && !string.IsNullOrEmpty(constVal))
                            {
                                rawUrl = constVal;
                            }
                            else if (ConfigurationIndex.TryGetConfig(null, propKey, out var confVal) && !string.IsNullOrEmpty(confVal))
                            {
                                rawUrl = confVal;
                            }
                        }
                        var norm = RouteDictionaryRegistry.NormalizeResolvedUrl(rawUrl);
                        if (!string.IsNullOrEmpty(norm) && norm != WildcardStr)
                        {
                            return norm;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(nameVal) && node is TypeDeclSyntax typeDecl)
            {
                nameVal = typeDecl.Name;
            }

            if (!string.IsNullOrEmpty(nameVal))
            {
                var combined = !string.IsNullOrEmpty(pathVal) ? $"{nameVal}/{pathVal.TrimStart(SlashChar)}" : nameVal;
                combined = ConfigurationIndex.ResolvePlaceholders(combined, projectName);
                return RouteDictionaryRegistry.NormalizeResolvedUrl(combined);
            }
        }

        if (DeclarativeUniversalRuleEvaluator.IsCallNode(targetNode, rule.CallNodeTypes))
        {
            var argList = DeclarativeSemanticExtension.FindArgumentsNode(targetNode);
            if (argList.IsValid())
            {
                foreach (var child in argList.Children)
                {
                    if (DeclarativeSemanticExtension.IsStringLiteral(child))
                    {
                        var text = child.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                        if (text.Contains(SlashChar) || text.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(text);
                        }
                    }
                    else if (child is IdentifierExprSyntax)
                    {
                        var varName = child.Text;
                        if (RouteDictionaryRegistry.TryResolve(varName, out var rPath, out var rService))
                        {
                            var cleanPath = rPath.Split(QuestionChar)[0];
                            return !string.IsNullOrEmpty(rService) ? $"{rService}{cleanPath}" : cleanPath;
                        }

                        var resolved = FindVariableInitializerInEnclosingScopes(child, varName);
                        if (!string.IsNullOrEmpty(resolved))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(resolved);
                        }
                    }
                    else if (child is BinaryExprSyntax)
                    {
                        var left = child.GetField(SyntaxFields.Left) ?? (child.Children.Count >= 1 ? child.Children[0] : null);
                        string? leftText = null;
                        if (left.IsValid())
                        {
                            if (DeclarativeSemanticExtension.IsStringLiteral(left))
                            {
                                leftText = left.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                            }
                            else if (left is IdentifierExprSyntax or CallExprSyntax)
                            {
                                var varName = left.Text.TrimStart(DollarChar);
                                if (left is CallExprSyntax)
                                {
                                    var callName = DeclarativeSemanticExtension.GetCallMethodName(left);
                                    if (!string.IsNullOrEmpty(callName)) varName = callName;
                                }
                                leftText = FindVariableInitializerInEnclosingScopes(child, varName);
                            }
                        }

                        var right = child.GetField(SyntaxFields.Right) ?? (child.Children.Count >= 3 ? child.Children[2] : null);
                        string? rightText = null;
                        if (right.IsValid() && DeclarativeSemanticExtension.IsStringLiteral(right))
                        {
                            rightText = right.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                        }

                        if (!string.IsNullOrEmpty(leftText) && !string.IsNullOrEmpty(rightText))
                        {
                            var combined = leftText.TrimEnd(SlashChar) + SlashStr + rightText.TrimStart(SlashChar);
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(combined);
                        }
                        if (!string.IsNullOrEmpty(leftText) && (leftText.Contains(SlashChar) || leftText.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase)))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(leftText.EndsWith(SlashChar) ? leftText + WildcardStr : leftText);
                        }
                        if (!string.IsNullOrEmpty(rightText))
                        {
                            return RouteDictionaryRegistry.NormalizeResolvedUrl(rightText);
                        }
                    }
                }
            }

            if (rule.ResolveFluentChains)
            {
                var trieUrl = CompiledCallChainTrie.TryExtractOutboundHttpTargetViaTrie(targetNode, rule);
                if (!string.IsNullOrEmpty(trieUrl))
                {
                    return trieUrl;
                }
            }
        }

        return rule.FallbackTarget;
    }

    internal static string? FindVariableInitializerInEnclosingScopes(Node node, string varName)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr is BlockStmtSyntax or FunctionDeclSyntax or TypeDeclSyntax || curr.IsAny(ClassBodyType, StatementBlockType, CompoundStatementType))
            {
                foreach (var child in curr.Children)
                {
                    if (child is VariableDeclSyntax or MemberDeclSyntax)
                    {
                        foreach (var decl in child.Children)
                        {
                            if (decl is not VariableDeclSyntax && decl.Type != PropertyElementType) continue;

                            var nameNode = decl.GetChildForField(SyntaxFields.Name) ??
                                           decl.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                            if (nameNode.IsValid() && nameNode.Text.TrimStart(DollarChar) == varName.TrimStart(DollarChar))
                            {
                                var valNode = decl.GetChildForField(SyntaxFields.Value) ??
                                              (decl.Children.Count >= 3 ? decl.Children[^1] : null);
                                if (valNode.IsValid())
                                {
                                    if (DeclarativeSemanticExtension.IsStringLiteral(valNode))
                                    {
                                        return valNode.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                                    }
                                    if (valNode is BinaryExprSyntax)
                                    {
                                        var left = valNode.GetChildForField(SyntaxFields.Left) ??
                                                   (valNode.Children.Count > 0 ? valNode.Children[0] : null);
                                        var right = valNode.GetChildForField(SyntaxFields.Right) ??
                                                    (valNode.Children.Count >= 3 ? valNode.Children[2] : null);

                                        if (left.IsValid() && DeclarativeSemanticExtension.IsStringLiteral(left))
                                        {
                                            var leftText = left.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                                            if (right.IsValid() && DeclarativeSemanticExtension.IsStringLiteral(right))
                                            {
                                                return leftText + right.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                                            }
                                            return leftText + WildcardStr;
                                        }
                                        if (right.IsValid() && DeclarativeSemanticExtension.IsStringLiteral(right))
                                        {
                                            return right.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                                        }
                                    }
                                    if (valNode is CallExprSyntax)
                                    {
                                        var argList = DeclarativeSemanticExtension.FindArgumentsNode(valNode);
                                        var str = argList?.Children.FirstOrDefault(DeclarativeSemanticExtension.IsStringLiteral);
                                        if (str.IsValid()) return str.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                                    }
                                }
                            }
                        }
                    }
                    if (child is FunctionDeclSyntax fn)
                    {
                        var fnName = fn.GetChildForField(SyntaxFields.Name)?.Text ?? fn.Children.FirstOrDefault(c => c is IdentifierExprSyntax)?.Text;
                        if (!string.IsNullOrEmpty(fnName) && fnName.Equals(varName, StringComparison.OrdinalIgnoreCase))
                        {
                            var str = fn.Descendants().FirstOrDefault(d => DeclarativeSemanticExtension.IsStringLiteral(d) && (d.Text.Contains(SlashChar) || d.Text.Contains(DashChar) || d.Text.Contains(ColonChar)));
                            if (str.IsValid())
                            {
                                var s = str.Text.Trim(DoubleQuoteChar, SingleQuoteChar);
                                if (!string.IsNullOrEmpty(s)) return s.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase) || s.StartsWith(LbPrefix, StringComparison.OrdinalIgnoreCase) ? s : $"http://{s}";
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }
        return null;
    }

    private static readonly string[] FunctionNodeTypes = ["method_declaration", "constructor_declaration", "function_declaration", "arrow_function"];
    private static readonly string[] TypeDeclNodeTypes = ["class_declaration", "interface_declaration", "record_declaration", "struct_declaration"];
    private const string ExtendsKeyword = "extends";
    private const string EmitterSub = "Emitter";
    private const string BaseListKind = "base_list";
    private const string MethodKey = "method";
    private const string ParametersSub = "parameters";
    private const string AnnotationArgListKind = "annotation_argument_list";
    private const string ArgumentsKind = "arguments";
    private const string ArgumentKind = "argument";
    private const string GetMethod = "GET";
    private const string PostMethod = "POST";
    private const string PutMethod = "PUT";
    private const string DeleteMethod = "DELETE";
    private const string PatchMethod = "PATCH";
    private const string HeadMethod = "HEAD";
    private const string OptionsMethod = "OPTIONS";
    private const char OpenBraceChar = '{';
    private const char CommaChar = ',';
    private const char DotChar = '.';

    internal static string? ExtractBaseClassName(Node node)
    {
        var superclass = node.Children.FirstOrDefault(c => c is BaseTypeClauseSyntax { IsImplements: false });
        if (superclass.IsValid())
        {
            var id = superclass.Children.FirstOrDefault(c => c is IdentifierExprSyntax or TypeRefSyntax);
            if (id.IsValid()) return id.Text;
            var text = superclass.Text.Replace(ExtendsKeyword, string.Empty).Trim();
            var angle = text.IndexOf(OpenAngleChar);
            if (angle > 0) text = text[..angle];
            return text.Trim();
        }
        return null;
    }

    internal static string? ExtractGenericTypeFromInterfaces(Node node, FrozenSet<string> repositoryInterfaceNames)
    {
        Node? ifaces = null;
        for (var i = 0; i < node.Children.Count; i++)
        {
            var c = node.Children[i];
            if (c is BaseTypeClauseSyntax { IsImplements: true } || c.Type == BaseListKind)
            {
                ifaces = c;
                break;
            }
        }

        var span = ifaces != null && ifaces.IsValid() ? ifaces.AsSpan() : node.AsSpan();
        var braceIdx = span.IndexOf(OpenBraceChar);
        if (braceIdx > 0) span = span[..braceIdx];

        foreach (var rName in repositoryInterfaceNames)
        {
            var idx = span.IndexOf(rName.AsSpan(), StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var afterMatch = span[idx..];
                var open = afterMatch.IndexOf(OpenAngleChar);
                var close = open > 0 ? afterMatch[open..].IndexOf(CloseAngleChar) : -1;
                if (open > 0 && close > 0)
                {
                    var genericParam = afterMatch.Slice(open + 1, close - 1).Trim();
                    var comma = genericParam.IndexOf(CommaChar);
                    if (comma > 0) genericParam = genericParam[..comma].Trim();
                    var lastDot = genericParam.LastIndexOf(DotChar);
                    if (lastDot >= 0 && lastDot + 1 < genericParam.Length)
                    {
                        genericParam = genericParam[(lastDot + 1)..].Trim();
                    }
                    return genericParam.ToString();
                }
            }
        }
        return null;
    }

    internal static bool IsChannelEmitter(Node node)
    {
        var parent = node.Parent;
        while (parent.IsValid())
        {
            var text = parent.Text;
            if (text.Contains(EmitterSub, StringComparison.OrdinalIgnoreCase)) return true;
            if (parent is FunctionDeclSyntax || parent.IsAny(FunctionNodeTypes) || parent is TypeDeclSyntax || parent.IsAny(TypeDeclNodeTypes))
            {
                break;
            }
            parent = parent.Parent;
        }
        return false;
    }

    internal static string? ExtractHttpMethodFromAnnotation(Node annotNode)
    {
        foreach (var child in annotNode.Children)
        {
            if (child is SyntaxList { Kind: SyntaxListKind.Arguments } || child.Type is AnnotationArgListKind or ArgumentsKind)
            {
                foreach (var argChild in child.Children)
                {
                    if (argChild is KeyValueExprSyntax or ArgumentSyntax || argChild.Type is ElementValuePairType or ArgumentKind)
                    {
                        var key = argChild.Children.FirstOrDefault(c => c is IdentifierExprSyntax) ?? argChild.GetField(SyntaxFields.Name);
                        if (key.IsValid() && string.Equals(key.Text, MethodKey, StringComparison.OrdinalIgnoreCase))
                        {
                            var matched = MatchHttpVerbInText(argChild.Text);
                            if (matched != null) return matched;
                        }
                    }
                }
            }
            else if (child is KeyValueExprSyntax || child.Type == ElementValuePairType)
            {
                var key = child.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                if (key.IsValid() && string.Equals(key.Text, MethodKey, StringComparison.OrdinalIgnoreCase))
                {
                    var matched = MatchHttpVerbInText(child.Text);
                    if (matched != null) return matched;
                }
            }
        }
        return null;
    }

    private static string? MatchHttpVerbInText(string text)
    {
        if (text.Contains(GetMethod, StringComparison.OrdinalIgnoreCase)) return GetMethod;
        if (text.Contains(PostMethod, StringComparison.OrdinalIgnoreCase)) return PostMethod;
        if (text.Contains(PutMethod, StringComparison.OrdinalIgnoreCase)) return PutMethod;
        if (text.Contains(DeleteMethod, StringComparison.OrdinalIgnoreCase)) return DeleteMethod;
        if (text.Contains(PatchMethod, StringComparison.OrdinalIgnoreCase)) return PatchMethod;
        if (text.Contains(HeadMethod, StringComparison.OrdinalIgnoreCase)) return HeadMethod;
        if (text.Contains(OptionsMethod, StringComparison.OrdinalIgnoreCase)) return OptionsMethod;
        return null;
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
                        ?? methodNode.Children.FirstOrDefault(c => c.Type.Contains(ParametersSub, StringComparison.Ordinal));
        if (!paramList.IsValid()) return null;

        var firstParam = paramList.Children.FirstOrDefault(c => c is ParameterDeclSyntax || c.Type.Contains(ParameterSub, StringComparison.Ordinal));
        if (!firstParam.IsValid()) return null;

        var typeNode = firstParam.GetField(SyntaxFields.Type);
        return typeNode.IsValid() ? typeNode.Text : null;
    }
}
