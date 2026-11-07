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
        if (rule.CallNodeTypes.Contains(node.Type))
        {
            var name = DeclarativeSemanticExtension.GetCallMethodName(node);
            if (!string.IsNullOrEmpty(name) && rule.CallMethodNames.Contains(name))
            {
                return true;
            }
        }

        if (rule.AnnotationNodeTypes.Contains(node.Type))
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

        if (rule.AnnotationNodeTypes.Contains(targetNode.Type))
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

        if (rule.CallNodeTypes.Contains(targetNode.Type))
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
}
