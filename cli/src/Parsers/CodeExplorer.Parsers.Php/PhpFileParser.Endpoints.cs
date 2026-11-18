using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parsers.Php;

public partial class PhpFileParser
{
    private const string ProtocolRest = "REST";
    private const string ProtocolHttp = "HTTP";
    private const string ProtocolMreg = "MREG";
    private const string ProtocolKjs = "KJS";
    private const string ProtocolSmtp = "SMTP";
    private const string MethodGet = "GET";
    private const string MethodPost = "POST";
    private const string MethodAny = "ANY";
    private const string MethodSend = "SEND";
    private const string PathKey = "path";
    private const string RouteKey = "route";
    private const string RouteTemplateKey = "route_template";
    private const string MethodKey = "method";
    private const string HttpMethodKey = "http_method";
    private const string FilePathKey = "file_path";
    private const string TargetKey = "target";
    private const string DomainOrServiceKey = "domain_or_service";
    private const string IsExternalKey = "is_external";
    private const string TrueString = "true";
    private const string MregServiceDomain = "mreg";
    private const string KjsServiceDomain = "kimjongsocket";
    private const string SmtpServiceDomain = "smtp-mailer";
    private const string PaymentServiceDomain = "payment-gateway";
    private const string KirbyControllersPath = "controllers/";
    private const string KirbyTemplatesPath = "templates/";
    private const string RootSlash = "/";
    private const string CommentEndMarker = "*/";
    private const string CommentStartMarker = "/**";
    private const string CmdGroup = "cmd";

    private static readonly Regex ParamDeclRegex = new(
        @"(?:\??(?<type>[a-zA-Z0-9_\\]+)\s+)?&?\$(?<name>[a-zA-Z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly Regex MethodCallRegex = new(
        @"(?:\$(?<var>[a-zA-Z0-9_]+)(?:->(?<prop>[a-zA-Z0-9_]+))?->|(?<static>[a-zA-Z0-9_\\]+)::)((?<method>[a-zA-Z0-9_]+))\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex NewInstanceRegex = new(
        @"\bnew\s+(?<type>[a-zA-Z0-9_\\]+)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex DirectFunctionCallRegex = new(
        @"(?<![a-zA-Z0-9_$>:\\])\b(?<fn>[a-zA-Z_][a-zA-Z0-9_]*)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex RouteAnnotationRegex = new(
        @"(?:@(?:[a-zA-Z0-9_\\]*Route)|#\[\s*Route)\s*\(\s*(?:(?:path|value)\s*[:=]\s*)?['""](?<path>[^'""]+)['""](?<rest>[^\)]*)\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RouteMethodsRegex = new(
        @"methods\s*[:=]\s*(?:\{|\[)\s*['""](?<verb>GET|POST|PUT|DELETE|PATCH)['""]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MregCallRegex = new(
        @"(?:(?:Mreg[_\\]Request|PTF_XClient)\s*(?:::|->|\()\s*(?:[a-zA-Z0-9_]+\s*\(\s*)?['""]?(?<cmd>[a-zA-Z0-9_.:-]+)['""]?|WL_Socket_Mreg::(?<cmd>[a-zA-Z_][a-zA-Z0-9_]*)\s*\(|WL_Socket_Request::sendCommand\s*\(\s*\\?WL_Socket_Request::MREG\s*,\s*(?:\\?[A-Za-z0-9_]+::)?['""]?(?<cmd>[a-zA-Z0-9_.:-]+)['""]?|->(?:sendCommand|sendRequest|executeCommand|callMreg)\s*\(\s*['""]?(?<cmd>[a-zA-Z0-9_.:-]+)['""]?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex KjsCallRegex = new(
        @"(?:WL_Socket_KimJong::(?<cmd>[a-zA-Z_][a-zA-Z0-9_]*)\s*\(|WL_Socket_Request::sendCommand\s*\(\s*\\?WL_Socket_Request::KIMJONG\s*,\s*(?:\\?WL_Socket_KimJong::)?['""]?(?<cmd>[a-zA-Z0-9_.:-]+)['""]?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TemplateRenderRegex = new(
        @"->\s*(?:display|fetch|render|renderView)\s*\(\s*['""](?<tpl>[^'""]+\.(?:tpl|twig|phtml|php))['""]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MailerUsageRegex = new(
        @"\b(?:Swift_Message|Swift_Mailer|PHPMailer|Nette\\Mail\\Message|->sendMail\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> IgnoredPhpKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "if", "elseif", "else", "for", "foreach", "while", "switch", "case", "catch", "return",
        "array", "isset", "empty", "unset", "list", "die", "exit", "eval", "print", "echo",
        "include", "include_once", "require", "require_once", "function", "class", "match"
    };

    private static readonly HashSet<string> Php8UpgradeRiskFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "each", "create_function", "count", "setlocale", "round", "number_format",
        "bcmul", "bcdiv", "bcadd", "bcsub", "usort", "uasort", "uksort", "sort", "rsort", "asort", "ksort"
    };

    private static List<PhpFunctionSpan> ParseFunctionsAndMethods(
        string content,
        string commentMasked,
        string masked,
        int[] lineOffsets,
        List<PhpClassScope> classScopes,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        Dictionary<string, string> useAliases,
        List<RawTypeBinding> rawTypeBindings)
    {
        _ = commentMasked;
        var spans = new List<PhpFunctionSpan>();

        foreach (Match fm in FunctionDeclRegex.Matches(masked))
        {
            var fnName = fm.Groups["name"].Value.Trim();
            var vis = fm.Groups["vis"].Success ? fm.Groups["vis"].Value.ToLowerInvariant() : VisibilityPublic;
            var (startLine, startCol) = GetLineAndColumn(lineOffsets, fm.Index);
            var (openBrace, closeBrace) = FindBodyBraces(masked, fm.Index + fm.Length);
            var endIdx = closeBrace > openBrace ? closeBrace : fm.Index + fm.Length;
            var (endLine, endCol) = GetLineAndColumn(lineOffsets, endIdx);

            var enclosingClass = FindEnclosingClass(fm.Index, classScopes);
            var symbolName = enclosingClass != null ? $"{enclosingClass.Name}.{fnName}" : fnName;
            var fnId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{symbolName}";
            var loc = Math.Max(1, endLine - startLine + 1);

            var fnNode = new FunctionNode(
                fnId,
                fnName,
                symbolName,
                relativePath,
                relativePath,
                startLine,
                endLine,
                startCol,
                endCol)
            {
                Loc = loc,
                Visibility = vis,
                IsExported = !vis.Equals(VisibilityPrivate, StringComparison.OrdinalIgnoreCase)
            };

            var rawParams = fm.Groups["params"].Value;
            var paramCount = ExtractFunctionParameters(rawParams, fnId, enclosingClass?.Node.Id, relativePath, startLine, fnNode, useAliases, rawTypeBindings);

            var bodySlice = openBrace >= 0 && closeBrace > openBrace
                ? masked.Substring(openBrace, closeBrace - openBrace + 1)
                : string.Empty;
            var rawBodySlice = openBrace >= 0 && closeBrace > openBrace
                ? content.Substring(openBrace, closeBrace - openBrace + 1)
                : string.Empty;

            var complexity = 1 + ComplexityBranchRegex.Matches(bodySlice).Count;
            fnNode.CustomProperties ??= new();
            fnNode.CustomProperties[ComplexityKey] = complexity.ToString();
            fnNode.CustomProperties[CyclomaticComplexityKey] = complexity.ToString();
            fnNode.CustomProperties[ParameterCountKey] = paramCount.ToString();

            if (enclosingClass != null)
            {
                enclosingClass.Node.Children.Add(fnNode);
                fnNode.Signals().EnclosingTypeName = enclosingClass.Name;
            }
            else
            {
                fileNode.Children.Add(fnNode);
            }

            if (!string.IsNullOrEmpty(rawBodySlice))
            {
                ExtractMethodCallsAndBindings(rawBodySlice, bodySlice, openBrace, lineOffsets, fnNode, enclosingClass, fileNode, relativePath, useAliases, rawTypeBindings);
            }

            spans.Add(new PhpFunctionSpan(fm.Index, endIdx, fnNode, enclosingClass));
        }

        return spans;
    }

    private static int ExtractFunctionParameters(
        string rawParams,
        string fnId,
        string? classTypeId,
        string relativePath,
        int line,
        FunctionNode fnNode,
        Dictionary<string, string> useAliases,
        List<RawTypeBinding> rawTypeBindings)
    {
        if (string.IsNullOrWhiteSpace(rawParams))
        {
            return 0;
        }

        var count = 0;
        foreach (Match pm in ParamDeclRegex.Matches(rawParams))
        {
            var pName = pm.Groups["name"].Value.Trim();
            var pType = NormalizePhpTypeName(pm.Groups["type"].Success ? pm.Groups["type"].Value : null, useAliases);
            count++;

            if (!string.IsNullOrEmpty(pType))
            {
                rawTypeBindings.Add(new RawTypeBinding(pName, pType, fnId, relativePath));
                if (fnNode.Name.Equals(ConstructMethodName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(classTypeId))
                {
                    rawTypeBindings.Add(new RawTypeBinding(pName, pType, classTypeId, relativePath));
                }
            }
        }
        _ = line;
        return count;
    }

    private static void ExtractMethodCallsAndBindings(
        string rawBodySlice,
        string maskedBodySlice,
        int bodyOffset,
        int[] lineOffsets,
        FunctionNode fnNode,
        PhpClassScope? enclosingClass,
        FileNode fileNode,
        string relativePath,
        Dictionary<string, string> useAliases,
        List<RawTypeBinding> rawTypeBindings)
    {
        foreach (Match nm in AssignmentNewRegex.Matches(rawBodySlice))
        {
            var varName = nm.Groups["var"].Value.Trim();
            var typeName = NormalizePhpTypeName(nm.Groups["type"].Value, useAliases);
            if (!string.IsNullOrEmpty(varName) && !string.IsNullOrEmpty(typeName))
            {
                rawTypeBindings.Add(new RawTypeBinding(varName, typeName, fnNode.Id, relativePath));
                if (enclosingClass != null)
                {
                    rawTypeBindings.Add(new RawTypeBinding(varName, typeName, enclosingClass.Node.Id, relativePath));
                }
            }
        }

        foreach (Match im in NewInstanceRegex.Matches(maskedBodySlice))
        {
            var instType = NormalizePhpTypeName(im.Groups["type"].Value, useAliases);
            if (!string.IsNullOrEmpty(instType) &&
                !instType.Equals(SelfReceiver, StringComparison.OrdinalIgnoreCase) &&
                !instType.Equals(StaticReceiver, StringComparison.OrdinalIgnoreCase))
            {
                var (callLine, callCol) = GetLineAndColumn(lineOffsets, bodyOffset + im.Index);
                fnNode.References.Add(new Reference(fnNode.Id, instType, OntologyConstants.Relationships.Instantiates, callLine, callCol));
                fnNode.References.Add(new Reference(fnNode.Id, instType, OntologyConstants.Relationships.UsesType, callLine, callCol));
            }
        }

        foreach (Match cm in MethodCallRegex.Matches(maskedBodySlice))
        {
            var methodName = cm.Groups["method"].Value.Trim();
            if (IgnoredPhpKeywords.Contains(methodName)) continue;

            var (callLine, callCol) = GetLineAndColumn(lineOffsets, bodyOffset + cm.Index);
            string? receiver = null;

            if (cm.Groups["static"].Success)
            {
                var rawStatic = cm.Groups["static"].Value.Trim();
                receiver = rawStatic.Equals(SelfReceiver, StringComparison.OrdinalIgnoreCase) ||
                           rawStatic.Equals(StaticReceiver, StringComparison.OrdinalIgnoreCase)
                    ? enclosingClass?.Name
                    : rawStatic.Equals(ParentReceiver, StringComparison.OrdinalIgnoreCase)
                        ? enclosingClass?.ExtendsName
                        : NormalizePhpTypeName(rawStatic, useAliases);
            }
            else if (cm.Groups["prop"].Success)
            {
                receiver = cm.Groups["prop"].Value.Trim();
            }
            else if (cm.Groups["var"].Success)
            {
                var v = cm.Groups["var"].Value.Trim();
                receiver = v.Equals(ThisReceiver, StringComparison.OrdinalIgnoreCase) ? enclosingClass?.Name : v;
            }

            var target = !string.IsNullOrEmpty(receiver) ? $"{receiver}.{methodName}" : methodName;
            fnNode.References.Add(new Reference(fnNode.Id, target, OntologyConstants.Relationships.Calls, callLine, callCol));
        }

        var risks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match dm in DirectFunctionCallRegex.Matches(maskedBodySlice))
        {
            var fnCall = dm.Groups["fn"].Value.Trim();
            if (IgnoredPhpKeywords.Contains(fnCall)) continue;

            var (callLine, callCol) = GetLineAndColumn(lineOffsets, bodyOffset + dm.Index);
            fnNode.References.Add(new Reference(fnNode.Id, fnCall, OntologyConstants.Relationships.Calls, callLine, callCol));
            if (Php8UpgradeRiskFunctions.Contains(fnCall))
            {
                risks.Add(fnCall.ToLowerInvariant());
            }
        }

        if (risks.Count > 0)
        {
            fnNode.CustomProperties ??= new();
            fnNode.CustomProperties[UpgradeRisksKey] = string.Join(CommaChar, risks);
        }
        _ = fileNode;
    }

    private static void ParseEndpointsAndExternalCalls(
        string content,
        string commentMasked,
        string masked,
        int[] lineOffsets,
        List<PhpClassScope> classScopes,
        List<PhpFunctionSpan> funSpans,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        List<RawImport> rawImports)
    {
        _ = commentMasked;
        _ = masked;

        foreach (var span in funSpans)
        {
            var fnNode = span.Node;
            var cls = span.EnclosingClass;
            var docBlock = ExtractPrecedingDocBlock(content, span.Start);
            var annotatedRoute = ExtractRouteFromDocBlockOrAttributes(docBlock, out var httpMethod);

            if (!string.IsNullOrEmpty(annotatedRoute))
            {
                var fullRoute = CombinePhpRoutes(cls?.RoutePrefix, annotatedRoute);
                AddPhpEndpoint(fileNode, workspaceId, relativePath, httpMethod, fullRoute, fnNode, fnNode.StartLine, fnNode.StartCol);
            }
            else if (cls is { IsController: true } &&
                     fnNode.Name.EndsWith(ActionSuffix, StringComparison.OrdinalIgnoreCase) &&
                     ! string.Equals(fnNode.Visibility, VisibilityPrivate, StringComparison.OrdinalIgnoreCase) &&
                     ! string.Equals(fnNode.Visibility, VisibilityProtected, StringComparison.OrdinalIgnoreCase))
            {
                var ctrlSlug = TrimSuffix(cls.Name, ControllerSuffix).Replace(UnderscoreChar, SlashChar).ToLowerInvariant();
                var actSlug = TrimSuffix(fnNode.Name, ActionSuffix).ToLowerInvariant();
                var zfRoute = CombinePhpRoutes(cls.RoutePrefix, $"/{ctrlSlug}/{actSlug}");
                AddPhpEndpoint(fileNode, workspaceId, relativePath, MethodAny, zfRoute, fnNode, fnNode.StartLine, fnNode.StartCol);
            }
        }

        if (relativePath.Contains(KirbyControllersPath, StringComparison.OrdinalIgnoreCase) ||
            relativePath.Contains(KirbyTemplatesPath, StringComparison.OrdinalIgnoreCase))
        {
            var pageSlug = Path.GetFileNameWithoutExtension(relativePath).ToLowerInvariant();
            var route = $"/{pageSlug}";
            var topFunc = funSpans.FirstOrDefault()?.Node;
            AddPhpEndpoint(fileNode, workspaceId, relativePath, MethodGet, route, topFunc, 1, 1);
        }

        foreach (Match mm in MregCallRegex.Matches(commentMasked))
        {
            var cmd = mm.Groups[CmdGroup].Value.Trim();
            if (string.IsNullOrWhiteSpace(cmd)) continue;

            var (line, col) = GetLineAndColumn(lineOffsets, mm.Index);
            var enclosingFn = FindEnclosingFunction(mm.Index, funSpans);
            var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{relativePath}:mreg:{cmd}:{line}";
            if (fileNode.Children.Any(c => c.Id == extId)) continue;

            var extNode = new ExternalServiceNode(extId, $"MREG {cmd}", ProtocolMreg, MregServiceDomain, cmd)
            {
                FilePath = relativePath,
                StartLine = line,
                StartCol = col,
                CallerSymbolId = enclosingFn?.Id,
                Url = cmd,
                Method = ProtocolMreg,
                IsExternal = true
            };
            extNode.CustomProperties ??= new();
            extNode.CustomProperties[FilePathKey] = relativePath;
            extNode.CustomProperties[TargetKey] = cmd;
            extNode.CustomProperties[DomainOrServiceKey] = MregServiceDomain;
            extNode.CustomProperties[IsExternalKey] = TrueString;
            fileNode.Children.Add(extNode);

            var callerId = enclosingFn?.Id ?? fileNode.Id;
            fileNode.References.Add(new Reference(callerId, extId, OntologyConstants.Relationships.Calls, line, col));
        }

        foreach (Match km in KjsCallRegex.Matches(commentMasked))
        {
            var cmd = km.Groups[CmdGroup].Value.Trim();
            if (string.IsNullOrWhiteSpace(cmd)) continue;

            var (line, col) = GetLineAndColumn(lineOffsets, km.Index);
            var enclosingFn = FindEnclosingFunction(km.Index, funSpans);
            var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{relativePath}:kjs:{cmd}:{line}";
            if (fileNode.Children.Any(c => c.Id == extId)) continue;

            var extNode = new ExternalServiceNode(extId, $"KJS {cmd}", ProtocolKjs, KjsServiceDomain, cmd)
            {
                FilePath = relativePath,
                StartLine = line,
                StartCol = col,
                CallerSymbolId = enclosingFn?.Id,
                Url = cmd,
                Method = ProtocolKjs,
                IsExternal = true
            };
            extNode.CustomProperties ??= new();
            extNode.CustomProperties[FilePathKey] = relativePath;
            extNode.CustomProperties[TargetKey] = cmd;
            extNode.CustomProperties[DomainOrServiceKey] = KjsServiceDomain;
            extNode.CustomProperties[IsExternalKey] = TrueString;
            fileNode.Children.Add(extNode);

            var callerId = enclosingFn?.Id ?? fileNode.Id;
            fileNode.References.Add(new Reference(callerId, extId, OntologyConstants.Relationships.Calls, line, col));
        }

        foreach (Match tm in TemplateRenderRegex.Matches(content))
        {
            var tplPath = tm.Groups["tpl"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(tplPath))
            {
                rawImports.Add(new RawImport(tplPath, relativePath, ImportType.Internal));
            }
        }

        foreach (Match sm in MailerUsageRegex.Matches(content))
        {
            var (line, col) = GetLineAndColumn(lineOffsets, sm.Index);
            var enclosingFn = FindEnclosingFunction(sm.Index, funSpans);
            var extId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{relativePath}:smtp:{line}";
            if (!fileNode.Children.Any(c => c.Id == extId))
            {
                var extNode = new ExternalServiceNode(extId, SmtpServiceDomain, ProtocolSmtp, SmtpServiceDomain, RootSlash)
                {
                    FilePath = relativePath,
                    StartLine = line,
                    StartCol = col,
                    CallerSymbolId = enclosingFn?.Id,
                    Method = MethodSend,
                    IsExternal = true
                };
                fileNode.Children.Add(extNode);
                var callerId = enclosingFn?.Id ?? fileNode.Id;
                fileNode.References.Add(new Reference(callerId, extId, OntologyConstants.Relationships.Calls, line, col));
            }
        }
    }

    private static void AddPhpEndpoint(
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        string httpMethod,
        string fullRoute,
        FunctionNode? handlerFunc,
        int line,
        int col)
    {
        var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{httpMethod}:{fullRoute}";
        if (fileNode.Children.Any(c => c.Id == epId))
        {
            return;
        }

        var epNode = new EndpointNode(
            epId,
            $"{httpMethod} {fullRoute}",
            relativePath,
            httpMethod,
            fullRoute,
            ProtocolRest)
        {
            FilePath = relativePath,
            StartLine = line,
            EndLine = line,
            Line = line,
            LineEnd = line,
            Operation = httpMethod,
            EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
        };
        epNode.CustomProperties ??= new();
        epNode.CustomProperties[PathKey] = fullRoute;
        epNode.CustomProperties[RouteKey] = fullRoute;
        epNode.CustomProperties[RouteTemplateKey] = fullRoute;
        epNode.CustomProperties[MethodKey] = httpMethod;
        epNode.CustomProperties[HttpMethodKey] = httpMethod;
        epNode.CustomProperties[FilePathKey] = relativePath;

        if (handlerFunc != null)
        {
            epNode.Children.Add(handlerFunc);
            fileNode.References.Add(new Reference(epId, handlerFunc.Id, OntologyConstants.Relationships.ExposedBy, line, col));
        }

        fileNode.Children.Add(epNode);
    }

    internal static string? ExtractPrecedingDocBlock(string content, int declIndex)
    {
        if (declIndex <= 0) return null;
        var windowStart = Math.Max(0, declIndex - 1500);
        var slice = content[windowStart..declIndex];
        var lastClose = slice.LastIndexOf(CommentEndMarker, StringComparison.Ordinal);
        if (lastClose >= 0)
        {
            var lastOpen = slice.LastIndexOf(CommentStartMarker, lastClose, StringComparison.Ordinal);
            if (lastOpen >= 0)
            {
                return slice[lastOpen..];
            }
        }
        return slice;
    }

    internal static string? ExtractRouteFromDocBlockOrAttributes(string? docBlock, out string httpMethod)
    {
        httpMethod = MethodGet;
        if (string.IsNullOrWhiteSpace(docBlock)) return null;

        var m = RouteAnnotationRegex.Match(docBlock);
        if (!m.Success) return null;

        var path = m.Groups["path"].Value.Trim();
        var rest = m.Groups["rest"].Value;
        var verbMatch = RouteMethodsRegex.Match(rest);
        if (verbMatch.Success)
        {
            httpMethod = verbMatch.Groups["verb"].Value.ToUpperInvariant();
        }
        return path;
    }

    internal static string CombinePhpRoutes(string? prefix, string? route)
    {
        var p = (prefix ?? string.Empty).Trim().TrimEnd(SlashChar);
        var r = (route ?? string.Empty).Trim().TrimStart(SlashChar);
        if (string.IsNullOrEmpty(p) && string.IsNullOrEmpty(r)) return RootSlash;
        if (string.IsNullOrEmpty(p)) return $"/{r}";
        if (string.IsNullOrEmpty(r)) return p.StartsWith(SlashChar) ? p : $"/{p}";
        var cleanP = p.StartsWith(SlashChar) ? p : $"/{p}";
        return $"{cleanP}/{r}";
    }

    private static string TrimSuffix(string value, string suffix)
    {
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && value.Length > suffix.Length
            ? value[..^suffix.Length]
            : value;
    }

    internal static PhpClassScope? FindEnclosingClass(int pos, List<PhpClassScope> classScopes)
    {
        foreach (var cs in classScopes)
        {
            if (cs.OpenBraceIndex >= 0 && cs.CloseBraceIndex >= 0 &&
                pos >= cs.OpenBraceIndex && pos <= cs.CloseBraceIndex)
            {
                return cs;
            }
        }
        return null;
    }

    internal static FunctionNode? FindEnclosingFunction(int pos, List<PhpFunctionSpan> funSpans)
    {
        foreach (var fs in funSpans)
        {
            if (pos >= fs.Start && pos <= fs.End)
            {
                return fs.Node;
            }
        }
        return null;
    }
}
