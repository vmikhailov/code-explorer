using System.Text;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser.Syntax;
using CodePassport.Engine.Models;

namespace CodePassport.Engine.Semantics;

/// <summary>
/// Evaluates Universal AST expressions (<see cref="ExprSyntax"/>, <see cref="SyntaxNode"/>)
/// into resolved symbolic bindings, folded string constants, decoded payloads
/// (<c>Buffer.from(..., 'base64')</c>, <c>atob(...)</c>, <c>String.fromCharCode(...)</c>),
/// and taint propagation vectors.
/// Operates directly on the lossless Universal AST hierarchy (both pre-mapped properties and structural child fields).
/// </summary>
public static partial class AstSymbolicEvaluator
{
    [GeneratedRegex(@"^(?:https?://|wss?://)?(?:\d{1,3}\.){3}\d{1,3}(?::\d+)?(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RawIpv4Regex();

    [GeneratedRegex(@"(?i)(ngrok\.io|ngrok-free\.app|oastify\.com|burpcollaborator\.net|interact\.sh|webhook\.site|pipedream\.net|requestbin\.com|discord(?:app)?\.com/api/webhooks|api\.telegram\.org/bot|pastebin\.com/raw|transfer\.sh)", RegexOptions.Compiled)]
    private static partial Regex ExfilHostRegex();

    [GeneratedRegex(@"(?i)(^|[\\/])(\.ssh|\.aws|\.kube|\.gnupg|\.npmrc|\.pypirc|\.docker|\.netrc|\.gitconfig|id_rsa|id_ed25519|known_hosts|credentials|\.env(\.local|\.production|\.development)?|solana[\\/]id\.json|MetaMask|Login Data|Default[\\/]Cookies|Network[\\/]Cookies)($|[\\/])", RegexOptions.Compiled)]
    private static partial Regex SensitivePathRegex();

    [GeneratedRegex(@"(?i)(curl\s+.*\|\s*(ba)?sh|wget\s+.*\|\s*(ba)?sh|powershell(?:\.exe)?\s+.*-enc|Invoke-WebRequest.*\|.*iex|certutil\s+-urlcache|/dev/tcp/)", RegexOptions.Compiled)]
    private static partial Regex ShellDropperRegex();

    [GeneratedRegex(@"(?i)(AWS_SECRET|AWS_ACCESS_KEY|GITHUB_TOKEN|NPM_TOKEN|OPENAI_API_KEY|ANTHROPIC_API_KEY|PRIVATE_KEY|MNEMONIC|SEED_PHRASE|DATABASE_URL|STRIPE_SECRET|SLACK_TOKEN|DISCORD_TOKEN|BEARER_TOKEN|API_SECRET|SECRET_KEY)", RegexOptions.Compiled)]
    private static partial Regex SecretEnvKeyRegex();

    [GeneratedRegex(@"(?i)^(CI|GITHUB_ACTIONS|GITLAB_CI|CIRCLECI|JENKINS_URL|TRAVIS|BUILDKITE|TF_BUILD)$", RegexOptions.Compiled)]
    private static partial Regex CiEnvKeyRegex();

    /// <summary>
    /// Extracts the argument child nodes of a <see cref="CallExprSyntax"/> from either
    /// <see cref="CallExprSyntax.Arguments"/> or the structural <c>arguments</c> / <c>argument_list</c> CST child.
    /// </summary>
    public static IReadOnlyList<SyntaxNode> GetCallArguments(CallExprSyntax callExpr)
    {
        if (callExpr.Arguments.Count > 0)
        {
            return callExpr.Arguments;
        }

        var argsContainer = callExpr.GetChildForField(SyntaxFields.Arguments) ??
                            callExpr.Children.FirstOrDefault(c => c.RawGrammarKind is "arguments" or "argument_list" or "value_arguments");
        if (argsContainer != null)
        {
            var list = new List<SyntaxNode>(argsContainer.NamedChildren.Count);
            foreach (var child in argsContainer.NamedChildren)
            {
                list.Add(child);
            }
            return list;
        }

        return [];
    }

    /// <summary>
    /// Evaluates an AST expression node within the given lexical symbol environment.
    /// </summary>
    public static SymbolicBinding? EvaluateExpression(
        SyntaxNode? node,
        IReadOnlyDictionary<string, SymbolicBinding> scopeEnv,
        int depth = 0)
    {
        if (node == null || depth > 16) return null;

        // Unwrap argument / parenthesized / await wrappers
        if (node is ArgumentSyntax arg)
        {
            var inner = arg.Value ?? arg.GetChildForField(SyntaxFields.Value) ?? arg.NamedChildren.FirstOrDefault();
            return EvaluateExpression(inner, scopeEnv, depth + 1);
        }
        if (node.RawGrammarKind is "parenthesized_expression" or "non_null_expression" or "as_expression" or "type_assertion")
        {
            return EvaluateExpression(node.NamedChildren.FirstOrDefault(), scopeEnv, depth + 1);
        }
        if (node is AwaitOrTryExprSyntax awaitExpr)
        {
            var inner = awaitExpr.Operand ?? awaitExpr.NamedChildren.FirstOrDefault();
            return EvaluateExpression(inner, scopeEnv, depth + 1);
        }
        if (node is AssignmentExprSyntax assignExpr)
        {
            var rhs = assignExpr.Right ?? assignExpr.GetChildForField(SyntaxFields.Right) ?? (assignExpr.NamedChildren.Count > 1 ? assignExpr.NamedChildren[1] : null);
            return EvaluateExpression(rhs, scopeEnv, depth + 1);
        }

        // 1. Literal expression
        if (node is LiteralExprSyntax lit)
        {
            if (lit.LiteralKind is LiteralSyntaxKind.String or LiteralSyntaxKind.RawString or LiteralSyntaxKind.Char)
            {
                var unquoted = AstValueResolver.Unquote(lit.ValueText.Length > 0 ? lit.ValueText : lit.Text);
                var litFlags = ClassifyResolvedStringConstant(unquoted, isDecoded: false);
                return new SymbolicBinding(
                    IntrinsicRole.GlobalObject,
                    CanonicalPath: string.Empty,
                    Capability: litFlags,
                    ResolvedConstant: unquoted);
            }
            return null;
        }

        // 2. Interpolated / template string: `https://${host}/collect`
        if (node is InterpolatedStringExprSyntax interp)
        {
            return EvaluateInterpolatedString(interp, scopeEnv, depth + 1);
        }

        // 3. Binary string concatenation: 'child_' + 'process' or host + '/api'
        if (node is BinaryExprSyntax bin)
        {
            var op = !string.IsNullOrEmpty(bin.Operator)
                ? bin.Operator
                : bin.GetChildForField(SyntaxFields.Operator)?.Text ?? string.Empty;

            if (op == "+" || bin.Text.Contains('+'))
            {
                var leftNode = bin.Left ?? bin.GetChildForField(SyntaxFields.Left) ?? (bin.NamedChildren.Count > 0 ? bin.NamedChildren[0] : null);
                var rightNode = bin.Right ?? bin.GetChildForField(SyntaxFields.Right) ?? (bin.NamedChildren.Count > 1 ? bin.NamedChildren[1] : null);
                var left = EvaluateExpression(leftNode, scopeEnv, depth + 1);
                var right = EvaluateExpression(rightNode, scopeEnv, depth + 1);
                if (left != null || right != null)
                {
                    string? folded = null;
                    if (left?.ResolvedConstant != null && right?.ResolvedConstant != null)
                    {
                        folded = left.ResolvedConstant + right.ResolvedConstant;
                    }
                    var isDecoded = (left?.IsDecodedFromObfuscation ?? false) || (right?.IsDecodedFromObfuscation ?? false);
                    var cap = (left?.Capability ?? CapabilityFlags.None) |
                              (right?.Capability ?? CapabilityFlags.None) |
                              (folded != null ? ClassifyResolvedStringConstant(folded, isDecoded) : CapabilityFlags.None);
                    var taint = (left?.TaintMask ?? CapabilityFlags.None) | (right?.TaintMask ?? CapabilityFlags.None);

                    return new SymbolicBinding(
                        IntrinsicRole.GlobalObject,
                        CanonicalPath: string.Empty,
                        Capability: cap,
                        ResolvedConstant: folded,
                        IsDecodedFromObfuscation: isDecoded,
                        SourceParameterIndex: left?.SourceParameterIndex ?? right?.SourceParameterIndex,
                        SourceParameterName: left?.SourceParameterName ?? right?.SourceParameterName,
                        TaintMask: taint,
                        TaintSourceExpr: left?.TaintSourceExpr ?? right?.TaintSourceExpr);
                }
            }
            else if (op is "||" or "&&" or "??")
            {
                var leftNode = bin.Left ?? bin.GetChildForField(SyntaxFields.Left) ?? (bin.NamedChildren.Count > 0 ? bin.NamedChildren[0] : null);
                var rightNode = bin.Right ?? bin.GetChildForField(SyntaxFields.Right) ?? (bin.NamedChildren.Count > 1 ? bin.NamedChildren[1] : null);
                var left = EvaluateExpression(leftNode, scopeEnv, depth + 1);
                var right = EvaluateExpression(rightNode, scopeEnv, depth + 1);
                if (left != null || right != null)
                {
                    var cap = (left?.Capability ?? CapabilityFlags.None) | (right?.Capability ?? CapabilityFlags.None);
                    var taint = (left?.TaintMask ?? CapabilityFlags.None) | (right?.TaintMask ?? CapabilityFlags.None);
                    if (cap != CapabilityFlags.None || taint != CapabilityFlags.None || left?.SourceParameterIndex != null || right?.SourceParameterIndex != null)
                    {
                        return new SymbolicBinding(
                            taint != CapabilityFlags.None ? IntrinsicRole.SensitiveSource : (left?.Role ?? right?.Role ?? IntrinsicRole.GlobalObject),
                            CanonicalPath: left?.CanonicalPath ?? right?.CanonicalPath ?? string.Empty,
                            Capability: cap,
                            SourceParameterIndex: left?.SourceParameterIndex ?? right?.SourceParameterIndex,
                            SourceParameterName: left?.SourceParameterName ?? right?.SourceParameterName,
                            TaintMask: taint,
                            TaintSourceExpr: left?.TaintSourceExpr ?? right?.TaintSourceExpr);
                    }
                }
            }
        }

        // 4. Identifier lookup in lexical symbol table or intrinsic ontology
        if (node is IdentifierExprSyntax idExpr)
        {
            var name = !string.IsNullOrEmpty(idExpr.Name) ? idExpr.Name : idExpr.Text.Trim();
            if (scopeEnv.TryGetValue(name, out var bound))
            {
                return bound;
            }
            if (IntrinsicSymbolOntology.TryResolveIntrinsicPath(name, out var intrinsic))
            {
                return intrinsic;
            }
            return null;
        }

        // 5. Member access: obj.prop (e.g., process.env, process.env.AWS_SECRET_KEY, cp.exec)
        if (node is MemberAccessExprSyntax memberExpr)
        {
            var objNode = memberExpr.Object ??
                          memberExpr.GetChildForField(SyntaxFields.Object) ??
                          (memberExpr.NamedChildren.Count > 0 ? memberExpr.NamedChildren[0] : null);
            var propNode = memberExpr.GetChildForField(SyntaxFields.Property) ??
                           memberExpr.GetChildForField(SyntaxFields.Name) ??
                           (memberExpr.NamedChildren.Count > 1 ? memberExpr.NamedChildren[^1] : null);

            var targetBinding = EvaluateExpression(objNode, scopeEnv, depth + 1);
            if (objNode != null &&
                targetBinding is { Role: IntrinsicRole.GlobalObject, SourceParameterIndex: null } &&
                string.IsNullOrEmpty(targetBinding.CanonicalPath))
            {
                var rawObjText = objNode.Text.Trim();
                if (rawObjText is not ("global" or "globalThis" or "window" or "self" or "process" or "document" or "navigator" or "Buffer" or "JSON" or "Object" or "String" or "WebAssembly" or "Reflect"))
                {
                    return null;
                }
            }
            var memberName = !string.IsNullOrEmpty(memberExpr.MemberName)
                ? memberExpr.MemberName
                : propNode?.Text ?? ExtractTrailingMemberName(memberExpr.Text);

            var projected = ResolvePropertyProjection(targetBinding, objNode?.Text, memberName);
            if (projected != null) return projected;

            if (!string.IsNullOrEmpty(memberName) && scopeEnv.TryGetValue($"prop:{memberName}", out var propBinding))
            {
                return propBinding;
            }

            return null;
        }

        // 6. Computed subscript access: obj[index] (e.g., globalThis['ev' + 'al'], process['env']['NPM_TOKEN'])
        if (node is IndexOrSliceExprSyntax indexExpr)
        {
            var targetNode = indexExpr.Target ??
                             indexExpr.GetChildForField(SyntaxFields.Object) ??
                             (indexExpr.NamedChildren.Count > 0 ? indexExpr.NamedChildren[0] : null);
            var indexChild = indexExpr.Index ??
                             indexExpr.GetChildForField(SyntaxFields.Index) ??
                             indexExpr.GetChildForField(SyntaxFields.Subscript) ??
                             (indexExpr.NamedChildren.Count > 1 ? indexExpr.NamedChildren[1] : null);

            var targetBinding = EvaluateExpression(targetNode, scopeEnv, depth + 1);
            var indexBinding = EvaluateExpression(indexChild, scopeEnv, depth + 1);
            var resolvedKey = indexBinding?.ResolvedConstant;

            if (!string.IsNullOrEmpty(resolvedKey))
            {
                var projected = ResolvePropertyProjection(targetBinding, targetNode?.Text, resolvedKey);
                if (projected != null)
                {
                    var extraCap = indexBinding?.IsDecodedFromObfuscation == true
                        ? CapabilityFlags.CryptoObfuscated
                        : CapabilityFlags.None;
                    return projected with
                    {
                        Capability = projected.Capability | extraCap,
                        IsDecodedFromObfuscation = projected.IsDecodedFromObfuscation || (indexBinding?.IsDecodedFromObfuscation ?? false)
                    };
                }
            }
            else if (targetBinding != null && indexBinding != null)
            {
                if (targetBinding.Role is IntrinsicRole.GlobalObject or IntrinsicRole.ModuleNamespace)
                {
                    return targetBinding with
                    {
                        Capability = targetBinding.Capability | CapabilityFlags.CodeReflect
                    };
                }
            }

            return targetBinding;
        }

        // 7. Call expression (require/import, Buffer.from, atob, String.fromCharCode, JSON.stringify, or source call)
        if (node is CallExprSyntax callExpr)
        {
            return EvaluateCallExpression(callExpr, scopeEnv, depth + 1);
        }

        // 8. Object / Array literal with spread or nested properties (e.g. { ...process.env } or { method: "POST", body: secret })
        if (node is CollectionExprSyntax collExpr)
        {
            return EvaluateCollectionExpression(collExpr, scopeEnv, depth + 1);
        }

        // 9. Conditional / Ternary expression (e.g. `opts.protocol === 'https:' ? https : http`)
        if (node is ConditionalExprSyntax condExpr)
        {
            foreach (var branch in condExpr.NamedChildren.Skip(1))
            {
                var branchBinding = EvaluateExpression(branch, scopeEnv, depth + 1);
                if (branchBinding != null)
                {
                    return branchBinding;
                }
            }
        }

        return null;
    }

    private static SymbolicBinding EvaluateInterpolatedString(
        InterpolatedStringExprSyntax interp,
        IReadOnlyDictionary<string, SymbolicBinding> scopeEnv,
        int depth)
    {
        var sb = new StringBuilder();
        var combinedTaint = CapabilityFlags.None;
        var combinedCap = CapabilityFlags.None;
        string? taintSrc = null;
        int? paramIdx = null;
        string? paramName = null;

        if (interp.Parts.Count > 0)
        {
            foreach (var part in interp.Parts)
            {
                if (!part.IsHole && part.LiteralText != null)
                {
                    sb.Append(part.LiteralText);
                }
                else if (part.Expression != null)
                {
                    var sub = EvaluateExpression(part.Expression, scopeEnv, depth + 1);
                    if (sub != null)
                    {
                        sb.Append(sub.ResolvedConstant ?? $"${{{sub.CanonicalPath}}}");
                        combinedCap |= sub.Capability;
                        combinedTaint |= sub.TaintMask;
                        taintSrc ??= sub.TaintSourceExpr;
                        paramIdx ??= sub.SourceParameterIndex;
                        paramName ??= sub.SourceParameterName;
                    }
                }
            }
        }
        else
        {
            // Fallback to structural children of template_string
            foreach (var child in interp.Children)
            {
                if (child.RawGrammarKind is "template_substitution" or "interpolation")
                {
                    var exprChild = child.NamedChildren.FirstOrDefault();
                    var sub = EvaluateExpression(exprChild, scopeEnv, depth + 1);
                    if (sub != null)
                    {
                        sb.Append(sub.ResolvedConstant ?? $"${{{sub.CanonicalPath}}}");
                        combinedCap |= sub.Capability;
                        combinedTaint |= sub.TaintMask;
                        taintSrc ??= sub.TaintSourceExpr;
                        paramIdx ??= sub.SourceParameterIndex;
                        paramName ??= sub.SourceParameterName;
                    }
                }
                else if (child.RawGrammarKind is "string_fragment" or "template_chars")
                {
                    sb.Append(child.Text);
                }
            }
        }

        var folded = sb.Length > 0 ? sb.ToString() : AstValueResolver.Unquote(interp.Text);
        combinedCap |= ClassifyResolvedStringConstant(folded, isDecoded: false);
        return new SymbolicBinding(
            IntrinsicRole.GlobalObject,
            CanonicalPath: string.Empty,
            Capability: combinedCap,
            ResolvedConstant: folded,
            SourceParameterIndex: paramIdx,
            SourceParameterName: paramName,
            TaintMask: combinedTaint,
            TaintSourceExpr: taintSrc);
    }

    private static SymbolicBinding? EvaluateCollectionExpression(
        CollectionExprSyntax collExpr,
        IReadOnlyDictionary<string, SymbolicBinding> scopeEnv,
        int depth)
    {
        var cap = CapabilityFlags.None;
        var taint = CapabilityFlags.None;
        string? taintSrc = null;
        int? paramIdx = null;
        string? paramName = null;
        string? moduleNamespacePath = null;

        // Inspect structural children (pair, spread_element, shorthand_property_identifier, etc.)
        foreach (var child in collExpr.NamedChildren)
        {
            if (child.RawGrammarKind == "spread_element")
            {
                var spreadTarget = child.NamedChildren.FirstOrDefault();
                var spreadBinding = EvaluateExpression(spreadTarget, scopeEnv, depth + 1);
                if (spreadBinding != null)
                {
                    cap |= spreadBinding.Capability;
                    taint |= spreadBinding.TaintMask;
                    taintSrc ??= spreadBinding.TaintSourceExpr;
                    if (spreadBinding.Role == IntrinsicRole.ModuleNamespace)
                    {
                        moduleNamespacePath ??= spreadBinding.CanonicalPath;
                    }
                    if (spreadBinding.CanonicalPath is "process.env" or "os.environ")
                    {
                        cap |= CapabilityFlags.EnvDumpAll;
                        taint |= CapabilityFlags.EnvDumpAll;
                        taintSrc ??= spreadBinding.CanonicalPath;
                    }
                }
            }
            else if (child.RawGrammarKind == "pair")
            {
                var valNode = child.GetChildForField(SyntaxFields.Value) ??
                              (child.NamedChildren.Count > 1 ? child.NamedChildren[^1] : null);
                var valBinding = EvaluateExpression(valNode, scopeEnv, depth + 1);
                if (valBinding != null)
                {
                    cap |= valBinding.Capability;
                    taint |= valBinding.TaintMask;
                    taintSrc ??= valBinding.TaintSourceExpr;
                    paramIdx ??= valBinding.SourceParameterIndex;
                    paramName ??= valBinding.SourceParameterName;
                    if (valBinding.Role == IntrinsicRole.ModuleNamespace)
                    {
                        moduleNamespacePath ??= valBinding.CanonicalPath;
                    }
                }
            }
            else
            {
                var elemBinding = EvaluateExpression(child, scopeEnv, depth + 1);
                if (elemBinding != null)
                {
                    cap |= elemBinding.Capability;
                    taint |= elemBinding.TaintMask;
                    taintSrc ??= elemBinding.TaintSourceExpr;
                    paramIdx ??= elemBinding.SourceParameterIndex;
                    paramName ??= elemBinding.SourceParameterName;
                    if (elemBinding.Role == IntrinsicRole.ModuleNamespace)
                    {
                        moduleNamespacePath ??= elemBinding.CanonicalPath;
                    }
                }
            }
        }

        if (moduleNamespacePath != null)
        {
            return new SymbolicBinding(
                IntrinsicRole.ModuleNamespace,
                CanonicalPath: moduleNamespacePath,
                Capability: cap,
                SourceParameterIndex: paramIdx,
                SourceParameterName: paramName,
                TaintMask: taint,
                TaintSourceExpr: taintSrc);
        }

        if (cap != CapabilityFlags.None || taint != CapabilityFlags.None || paramIdx != null)
        {
            return new SymbolicBinding(
                taint != CapabilityFlags.None ? IntrinsicRole.SensitiveSource : IntrinsicRole.GlobalObject,
                CanonicalPath: "collection",
                Capability: cap,
                SourceParameterIndex: paramIdx,
                SourceParameterName: paramName,
                TaintMask: taint,
                TaintSourceExpr: taintSrc);
        }

        return null;
    }

    public static SymbolicBinding? ResolvePropertyProjection(
        SymbolicBinding? receiverBinding,
        string? rawReceiverText,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return receiverBinding;

        var basePath = receiverBinding?.CanonicalPath;
        if (string.IsNullOrEmpty(basePath) && !string.IsNullOrWhiteSpace(rawReceiverText))
        {
            basePath = rawReceiverText.Trim();
        }

        if (string.IsNullOrEmpty(basePath)) return null;

        // Special handling for environment variable property access: process.env.<KEY> or os.environ.<KEY>
        if (basePath is "process.env" or "os.environ")
        {
            if (SecretEnvKeyRegex().IsMatch(propertyName))
            {
                return new SymbolicBinding(
                    IntrinsicRole.SensitiveSource,
                    $"{basePath}.{propertyName}",
                    CapabilityFlags.EnvSecretRead,
                    TaintMask: CapabilityFlags.EnvSecretRead,
                    TaintSourceExpr: $"{basePath}.{propertyName}");
            }
            if (CiEnvKeyRegex().IsMatch(propertyName))
            {
                return new SymbolicBinding(
                    IntrinsicRole.SensitiveSource,
                    $"{basePath}.{propertyName}",
                    CapabilityFlags.EnvCiEvasion,
                    TaintMask: CapabilityFlags.EnvCiEvasion,
                    TaintSourceExpr: $"{basePath}.{propertyName}");
            }
            return new SymbolicBinding(
                IntrinsicRole.SensitiveSource,
                $"{basePath}.{propertyName}",
                CapabilityFlags.EnvConfig,
                TaintMask: CapabilityFlags.EnvConfig,
                TaintSourceExpr: $"{basePath}.{propertyName}");
        }

        var composedPath = $"{basePath}.{propertyName}";
        if (IntrinsicSymbolOntology.TryResolveIntrinsicPath(composedPath, out var resolvedIntrinsic))
        {
            if (resolvedIntrinsic.Role == IntrinsicRole.SensitiveSource)
            {
                return resolvedIntrinsic with
                {
                    TaintMask = resolvedIntrinsic.Capability,
                    TaintSourceExpr = composedPath
                };
            }
            return resolvedIntrinsic;
        }

        if (receiverBinding?.SourceParameterIndex != null)
        {
            return receiverBinding with
            {
                CanonicalPath = composedPath
            };
        }

        if (receiverBinding != null && receiverBinding.TaintMask != CapabilityFlags.None)
        {
            return receiverBinding with
            {
                CanonicalPath = composedPath
            };
        }

        return null;
    }

    private static SymbolicBinding? EvaluateCallExpression(
        CallExprSyntax callExpr,
        IReadOnlyDictionary<string, SymbolicBinding> scopeEnv,
        int depth)
    {
        var calleeBinding = ResolveCallTargetBinding(callExpr, scopeEnv, depth);
        var rawMethod = callExpr.MethodName;
        var args = GetCallArguments(callExpr);

        // 1. Dynamic require(...) / import(...) resolution
        if (rawMethod is "require" or "import" || calleeBinding?.CanonicalPath is "require" or "import")
        {
            if (args.Count > 0)
            {
                var arg0 = EvaluateExpression(args[0], scopeEnv, depth + 1);
                if (arg0?.ResolvedConstant != null)
                {
                    var extraObf = arg0.IsDecodedFromObfuscation ? CapabilityFlags.CryptoObfuscated : CapabilityFlags.None;
                    if (IntrinsicSymbolOntology.TryResolveModule(arg0.ResolvedConstant, out var modBinding))
                    {
                        return modBinding with
                        {
                            Capability = modBinding.Capability | extraObf,
                            IsDecodedFromObfuscation = arg0.IsDecodedFromObfuscation
                        };
                    }
                    return new SymbolicBinding(
                        IntrinsicRole.ModuleNamespace,
                        IntrinsicSymbolOntology.NormalizeModuleName(arg0.ResolvedConstant),
                        Capability: extraObf,
                        IsDecodedFromObfuscation: arg0.IsDecodedFromObfuscation);
                }
                else
                {
                    return new SymbolicBinding(
                        IntrinsicRole.CapabilitySink,
                        "require(<computed>)",
                        CapabilityFlags.CodeComputedImport);
                }
            }
        }

        // 1b. TypeScript / Babel CommonJS wrapper: __importStar(require("net")) or __importDefault(require("debug"))
        if (rawMethod is "__importStar" or "__importDefault" && args.Count > 0)
        {
            return EvaluateExpression(args[0], scopeEnv, depth + 1);
        }

        // 1c. Object.defineProperty / Object.setPrototypeOf: only CODE.MONKEY_PATCH if target is a global/built-in prototype
        if (calleeBinding?.CanonicalPath is "Object.defineProperty" or "Object.setPrototypeOf" or "Reflect.setPrototypeOf" ||
            (rawMethod is "defineProperty" or "setPrototypeOf" && callExpr.Receiver?.Text is "Object" or "Reflect"))
        {
            if (args.Count > 0)
            {
                var targetText = args[0].Text.Trim();
                if ((targetText.Contains(".prototype", StringComparison.Ordinal) &&
                     !targetText.StartsWith("this.", StringComparison.Ordinal) &&
                     !targetText.StartsWith("newTarget.", StringComparison.Ordinal)) ||
                    targetText is "global" or "globalThis" or "window" or "process" or "Module")
                {
                    var canon = rawMethod == "setPrototypeOf" ? "Object.setPrototypeOf" : "Object.defineProperty";
                    return new SymbolicBinding(IntrinsicRole.CapabilitySink, canon, CapabilityFlags.CodeMonkeyPatch);
                }
            }
            return null;
        }

        // 2. Constant Decode Folding: Buffer.from(str, 'base64').toString('utf8') or Buffer.from(str, 'hex')
        if (rawMethod == "toString" && callExpr.Receiver is CallExprSyntax innerBufferCall)
        {
            var decoded = TryEvaluateBufferFrom(innerBufferCall, scopeEnv, depth + 1);
            if (decoded != null) return decoded;
        }

        var directBuffer = TryEvaluateBufferFrom(callExpr, scopeEnv, depth);
        if (directBuffer != null) return directBuffer;

        // 3. Constant Decode Folding: atob("...")
        if (rawMethod == "atob" || calleeBinding?.CanonicalPath == "atob")
        {
            if (args.Count > 0)
            {
                var arg0 = EvaluateExpression(args[0], scopeEnv, depth + 1);
                if (arg0?.ResolvedConstant != null && TryDecodeBase64(arg0.ResolvedConstant, out var decodedStr))
                {
                    var flags = CapabilityFlags.CryptoObfuscated | ClassifyResolvedStringConstant(decodedStr, isDecoded: true);
                    return new SymbolicBinding(
                        IntrinsicRole.DecodeTransform,
                        "atob",
                        Capability: flags,
                        ResolvedConstant: decodedStr,
                        IsDecodedFromObfuscation: true);
                }
            }
        }

        // 4. Constant Decode Folding: String.fromCharCode(99, 104, 105, ...)
        if (calleeBinding?.CanonicalPath == "String.fromCharCode" ||
            (rawMethod == "fromCharCode" && callExpr.Receiver?.Text == "String"))
        {
            var chars = new List<char>();
            foreach (var arg in args)
            {
                var text = arg.Text.Trim();
                if (int.TryParse(text, out var code) && code is > 0 and < 65536)
                {
                    chars.Add((char)code);
                }
            }
            if (chars.Count > 0)
            {
                var decodedStr = new string([.. chars]);
                var flags = CapabilityFlags.CryptoObfuscated | ClassifyResolvedStringConstant(decodedStr, isDecoded: true);
                return new SymbolicBinding(
                    IntrinsicRole.DecodeTransform,
                    "String.fromCharCode",
                    Capability: flags,
                    ResolvedConstant: decodedStr,
                    IsDecodedFromObfuscation: true);
            }
        }

        // 5. Serialization transform: JSON.stringify(process.env) / Object.entries(process.env)
        if (calleeBinding?.Role == IntrinsicRole.SerializeTransform)
        {
            var combinedCap = CapabilityFlags.None;
            var combinedTaint = CapabilityFlags.None;
            string? taintSrc = null;

            foreach (var arg in args)
            {
                var argBinding = EvaluateExpression(arg, scopeEnv, depth + 1);
                if (argBinding != null)
                {
                    combinedCap |= argBinding.Capability;
                    combinedTaint |= argBinding.TaintMask;
                    taintSrc ??= argBinding.TaintSourceExpr;
                    if (argBinding.CanonicalPath is "process.env" or "os.environ")
                    {
                        combinedCap |= CapabilityFlags.EnvDumpAll;
                        combinedTaint |= CapabilityFlags.EnvDumpAll;
                        taintSrc = argBinding.CanonicalPath;
                    }
                }
            }

            if (combinedTaint == CapabilityFlags.None)
            {
                return null;
            }

            return new SymbolicBinding(
                IntrinsicRole.SensitiveSource,
                calleeBinding.CanonicalPath,
                Capability: combinedCap,
                TaintMask: combinedTaint,
                TaintSourceExpr: taintSrc);
        }

        // 6. Sensitive source call (e.g., fs.readFileSync("~/.ssh/id_rsa"), os.userInfo(), localStorage.getItem("token"))
        if (calleeBinding?.Role == IntrinsicRole.SensitiveSource)
        {
            var cap = calleeBinding.Capability;
            var taint = calleeBinding.Capability & ~CapabilityFlags.FsRead;
            var srcExpr = calleeBinding.CanonicalPath;

            foreach (var arg in args)
            {
                var argBinding = EvaluateExpression(arg, scopeEnv, depth + 1);
                if (argBinding != null)
                {
                    cap |= argBinding.Capability;
                    const CapabilityFlags nonSecretPathFlags = CapabilityFlags.FsRead | CapabilityFlags.EnvConfig | CapabilityFlags.EnvFingerprint;
                    taint |= (argBinding.Capability & ~nonSecretPathFlags) | (argBinding.TaintMask & ~nonSecretPathFlags);
                    if (!string.IsNullOrEmpty(argBinding.ResolvedConstant))
                    {
                        srcExpr = $"{calleeBinding.CanonicalPath}('{argBinding.ResolvedConstant}')";
                    }
                }
            }

            return new SymbolicBinding(
                taint != CapabilityFlags.None ? IntrinsicRole.SensitiveSource : IntrinsicRole.GlobalObject,
                calleeBinding.CanonicalPath,
                Capability: cap,
                TaintMask: taint,
                TaintSourceExpr: taint != CapabilityFlags.None ? srcExpr : null);
        }

        // Benign standard global-this resolution idiom: `Function("return this")` or `Function("return this")()`
        if (calleeBinding?.CanonicalPath == "Function" && !calleeBinding.IsDecodedFromObfuscation)
        {
            if (args.Count == 1 &&
                EvaluateExpression(args[0], scopeEnv, depth + 1) is { IsDecodedFromObfuscation: false, ResolvedConstant: { } fnBody } &&
                fnBody.Trim() is "return this" or "return this;")
            {
                return new SymbolicBinding(IntrinsicRole.GlobalObject, "globalThis");
            }

            if (args.Count == 0 &&
                (callExpr.Callee ?? callExpr.GetChildForField(SyntaxFields.Function)) is CallExprSyntax innerFnCall)
            {
                var innerEval = EvaluateExpression(innerFnCall, scopeEnv, depth + 1);
                if (innerEval is { Role: IntrinsicRole.GlobalObject, CanonicalPath: "globalThis" })
                {
                    return innerEval;
                }
            }

            // Calling `new Function(...)` or `Function(...)` returns a compiled closure instance, not the `Function` constructor sink itself
            return new SymbolicBinding(IntrinsicRole.LocalFunctionRef, "Function.instance");
        }

        if (calleeBinding?.Role == IntrinsicRole.ModuleNamespace)
        {
            return null;
        }

        return calleeBinding;
    }

    public static SymbolicBinding? ResolveCallTargetBinding(
        CallExprSyntax callExpr,
        IReadOnlyDictionary<string, SymbolicBinding> scopeEnv,
        int depth = 0)
    {
        var calleeNode = callExpr.Callee ??
                         callExpr.GetChildForField(SyntaxFields.Function) ??
                         callExpr.GetChildForField(SyntaxFields.Constructor);
        if (calleeNode != null)
        {
            var direct = EvaluateExpression(calleeNode, scopeEnv, depth + 1);
            if (direct != null) return direct;
        }

        if (callExpr.Receiver != null && !string.IsNullOrEmpty(callExpr.MethodName))
        {
            var recv = EvaluateExpression(callExpr.Receiver, scopeEnv, depth + 1);
            if (recv is { Role: IntrinsicRole.GlobalObject, SourceParameterIndex: null } &&
                string.IsNullOrEmpty(recv.CanonicalPath))
            {
                var rawRecvText = callExpr.Receiver.Text.Trim();
                if (rawRecvText is not ("global" or "globalThis" or "window" or "self" or "process" or "document" or "navigator" or "Buffer" or "JSON" or "Object" or "String" or "WebAssembly" or "Reflect"))
                {
                    return null;
                }
            }
            var proj = ResolvePropertyProjection(recv, callExpr.Receiver.Text, callExpr.MethodName);
            if (proj != null) return proj;
            return null;
        }

        if (callExpr.Receiver == null && !string.IsNullOrEmpty(callExpr.MethodName))
        {
            if (scopeEnv.TryGetValue(callExpr.MethodName, out var bound))
            {
                return bound;
            }
            if (IntrinsicSymbolOntology.TryResolveIntrinsicPath(callExpr.MethodName, out var intrinsic))
            {
                return intrinsic;
            }
        }

        return null;
    }

    private static SymbolicBinding? TryEvaluateBufferFrom(
        CallExprSyntax callExpr,
        IReadOnlyDictionary<string, SymbolicBinding> scopeEnv,
        int depth)
    {
        var isBufferFrom = callExpr.MethodName == "from" &&
                           (callExpr.Receiver?.Text == "Buffer" ||
                            (callExpr.Callee?.Text.StartsWith("Buffer.from", StringComparison.Ordinal) ?? false));

        var args = GetCallArguments(callExpr);
        if (!isBufferFrom || args.Count == 0) return null;

        var payloadArg = EvaluateExpression(args[0], scopeEnv, depth + 1);
        var payload = payloadArg?.ResolvedConstant;
        if (string.IsNullOrEmpty(payload)) return null;

        var encoding = "utf8";
        if (args.Count >= 2)
        {
            var encArg = EvaluateExpression(args[1], scopeEnv, depth + 1);
            if (!string.IsNullOrEmpty(encArg?.ResolvedConstant))
            {
                encoding = encArg.ResolvedConstant.ToLowerInvariant();
            }
        }

        if (encoding is "base64" or "base64url" && TryDecodeBase64(payload, out var decodedB64))
        {
            var flags = CapabilityFlags.CryptoObfuscated | ClassifyResolvedStringConstant(decodedB64, isDecoded: true);
            return new SymbolicBinding(
                IntrinsicRole.DecodeTransform,
                "Buffer.from(base64)",
                Capability: flags,
                ResolvedConstant: decodedB64,
                IsDecodedFromObfuscation: true);
        }

        if (encoding == "hex" && TryDecodeHex(payload, out var decodedHex))
        {
            var flags = CapabilityFlags.CryptoObfuscated | ClassifyResolvedStringConstant(decodedHex, isDecoded: true);
            return new SymbolicBinding(
                IntrinsicRole.DecodeTransform,
                "Buffer.from(hex)",
                Capability: flags,
                ResolvedConstant: decodedHex,
                IsDecodedFromObfuscation: true);
        }

        return null;
    }

    public static CapabilityFlags ClassifyResolvedStringConstant(string value, bool isDecoded)
    {
        if (string.IsNullOrWhiteSpace(value)) return CapabilityFlags.None;
        var val = value.Trim();
        var mask = isDecoded ? CapabilityFlags.CryptoObfuscated : CapabilityFlags.None;

        if (RawIpv4Regex().IsMatch(val))
        {
            mask |= CapabilityFlags.NetRawIp | CapabilityFlags.NetHttpClient;
        }
        if (ExfilHostRegex().IsMatch(val))
        {
            mask |= CapabilityFlags.NetExfilHost | CapabilityFlags.NetHttpClient;
        }
        if (SensitivePathRegex().IsMatch(val))
        {
            mask |= CapabilityFlags.FsSensitivePath;
        }
        if (val.Contains("../..", StringComparison.Ordinal) || val.Contains(@"..\..", StringComparison.Ordinal))
        {
            mask |= CapabilityFlags.FsTraversal;
        }
        if (ShellDropperRegex().IsMatch(val))
        {
            mask |= CapabilityFlags.ProcShellDropper | CapabilityFlags.ProcSpawn;
        }
        if (IsHighEntropyBlob(val))
        {
            mask |= CapabilityFlags.CryptoObfuscated;
        }

        if (val is "eth_sendTransaction" or "eth_signTypedData" or "eth_signTypedData_v4" or "personal_sign" or "wallet_addEthereumChain")
        {
            mask |= CapabilityFlags.DomWeb3;
        }

        return mask;
    }

    public static bool IsHighEntropyBlob(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 120) return false;
        if (value.Contains(' ') || value.Contains('\n')) return false;

        Span<int> counts = stackalloc int[128];
        var total = 0;
        foreach (var ch in value)
        {
            if (ch < 128)
            {
                counts[ch]++;
                total++;
            }
        }
        if (total < 120) return false;

        var entropy = 0.0;
        for (var i = 0; i < 128; i++)
        {
            if (counts[i] == 0) continue;
            var p = (double)counts[i] / total;
            entropy -= p * Math.Log2(p);
        }

        return entropy >= 4.65;
    }

    private static bool TryDecodeBase64(string input, out string decoded)
    {
        decoded = string.Empty;
        try
        {
            var bytes = Convert.FromBase64String(input.Trim());
            if (bytes.Length == 0) return false;
            decoded = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDecodeHex(string input, out string decoded)
    {
        decoded = string.Empty;
        try
        {
            var bytes = Convert.FromHexString(input.Trim());
            if (bytes.Length == 0) return false;
            decoded = Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractTrailingMemberName(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        var dot = rawText.LastIndexOf('.');
        return dot >= 0 ? rawText[(dot + 1)..].Trim() : rawText.Trim();
    }
}
