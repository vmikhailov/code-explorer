using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer3SyntacticParser
{
    private static bool IsEnclosedInProject(FileNode file, ProjectNode project, List<ProjectNode> projects) =>
        Layer2ProjectParser.IsEnclosedInProject(file, project, projects);

    private static string? FindParentId(IOntologyNode root, string childId)
    {
        foreach (var child in root.Children)
        {
            if (child.Id == childId) return root.Id;
            var parentId = FindParentId(child, childId);
            if (parentId != null) return parentId;
        }
        return null;
    }

    private static IOntologyNode? FindNodeById(IOntologyNode root, string id)
    {
        if (root.Id == id) return root;
        foreach (var child in root.Children)
        {
            var found = FindNodeById(child, id);
            if (found != null) return found;
        }
        return null;
    }

    private static CallLiteralNode CreateCallLiteralNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId,
        SyntacticSymbol syntactic,
        bool symbolIsTest,
        string? projectKind,
        string? projectName)
    {
        var val = syntactic.Properties.GetValueOrDefault("value") ?? syntactic.Name;
        var callee = syntactic.Properties.GetValueOrDefault("callee") ?? "";
        var argIdx = int.TryParse(syntactic.Properties.GetValueOrDefault("arg_index"), out var ai) ? ai : 0;
        var line = int.TryParse(syntactic.Properties.GetValueOrDefault("line"), out var ln) ? ln : node.StartPosition.Row + 1;
        var col = int.TryParse(syntactic.Properties.GetValueOrDefault("column"), out var cl) ? cl : node.StartPosition.Column + 1;
        var role = syntactic.Properties.GetValueOrDefault("role") ?? CallSemanticRole.Business;
        var isDecorator = bool.TryParse(syntactic.Properties.GetValueOrDefault("is_decorator"), out var isDec) && isDec;
        var litId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CallLiteral}:{relativePath}:{line}:{col}";

        return new CallLiteralNode(litId, name, val, callee, argIdx, relativePath, line, col, symbolIsTest, projectKind, projectName, role, isDecorator);
    }

    private static CatchClauseNode CreateCatchClauseNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId,
        SyntacticSymbol syntactic,
        bool symbolIsTest,
        string? projectKind,
        string? projectName)
    {
        var exType = syntactic.Properties.GetValueOrDefault("exception_type") ?? "";
        var isEmpty = bool.TryParse(syntactic.Properties.GetValueOrDefault("is_empty"), out var ie) && ie;
        var hasComment = bool.TryParse(syntactic.Properties.GetValueOrDefault("has_comment"), out var hc) && hc;
        var stmtCount = int.TryParse(syntactic.Properties.GetValueOrDefault("statement_count"), out var sc) ? sc : 0;
        var line = int.TryParse(syntactic.Properties.GetValueOrDefault("line"), out var ln) ? ln : node.StartPosition.Row + 1;
        var col = int.TryParse(syntactic.Properties.GetValueOrDefault("column"), out var cl) ? cl : node.StartPosition.Column + 1;
        var catchId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CatchClause}:{relativePath}:{line}:{col}";

        return new CatchClauseNode(catchId, name, exType, relativePath, line, col, isEmpty, hasComment, stmtCount, symbolIsTest, projectKind, projectName);
    }

    private static string CombineRoutes(string prefix, string route)
    {
        prefix = (prefix ?? "").Trim('/');
        route = (route ?? "").Trim('/');
        if (string.IsNullOrEmpty(prefix)) return "/" + route;
        if (string.IsNullOrEmpty(route)) return "/" + prefix;
        return $"/{prefix}/{route}";
    }

    private static EndpointNode CreateEndpointNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId,
        SyntacticSymbol? syntactic = null,
        string? projectName = null)
    {
        var idx = name.IndexOf(':');
        var method = idx > 0 ? name[..idx].ToUpperInvariant() : (syntactic?.Protocol == "gRPC" ? "RPC" : "GET");
        var route = idx > 0 ? name[(idx + 1)..] : name;

        var resolvedProj = !string.IsNullOrEmpty(projectName) ? projectName : GetProjectNameFromRelativePath(relativePath);
        if (!string.IsNullOrEmpty(resolvedProj) &&
            (resolvedProj.StartsWith("internal-service-", StringComparison.OrdinalIgnoreCase) ||
             resolvedProj.StartsWith("integration-service-", StringComparison.OrdinalIgnoreCase) ||
             resolvedProj.StartsWith("external-service-", StringComparison.OrdinalIgnoreCase)))
        {
            var stripped = WorkspaceConventions.NormalizeServiceName(resolvedProj);
            if (!string.IsNullOrEmpty(stripped)) resolvedProj = stripped;
        }

        var protocol = syntactic?.Protocol ?? (method is "RPC" or "GRPC" ? "gRPC" : (method is "GRAPHQL" or "QUERY" or "MUTATION" or "SUBSCRIPTION" ? "GraphQL" : "REST"));
        var operationType = syntactic?.OperationType ?? (protocol == "GraphQL"
            ? (method is "MUTATION" ? "Mutation" : (method is "SUBSCRIPTION" ? "Subscription" : "Query"))
            : (protocol == "gRPC" ? "Unary" : null));

        // Apply project global route prefix (e.g. 'api/v1' or 'api') if discovered and not already present (REST only)
        if (protocol == "REST" &&
            !string.IsNullOrEmpty(resolvedProj) &&
            RouteDictionaryRegistry.TryGetGlobalPrefix(resolvedProj, out var globalPrefix) &&
            !string.IsNullOrEmpty(globalPrefix))
        {
            var normRoute = route.Trim('/');
            if (!normRoute.StartsWith(globalPrefix, StringComparison.OrdinalIgnoreCase) &&
                !normRoute.Equals("ping", StringComparison.OrdinalIgnoreCase))
            {
                route = CombineRoutes(globalPrefix, route);
                name = $"{method}:{route}";
            }
        }

        var projPart = (!string.IsNullOrEmpty(resolvedProj) && resolvedProj != "default") ? $"{resolvedProj}:" : "";
        var schemaPart = syntactic != null &&
                         syntactic.Properties.TryGetValue("graphql_schema", out var gqlSchema) &&
                         !string.IsNullOrEmpty(gqlSchema)
            ? $"{gqlSchema}:"
            : "";
        var endpointId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{projPart}{schemaPart}{method}:{route}";
        return new EndpointNode(
            endpointId,
            name,
            relativePath,
            method,
            route,
            protocol,
            syntactic?.IsAnonymous ?? false,
            syntactic?.RequiredRoles,
            syntactic?.Policies,
            syntactic?.RequestType,
            syntactic?.ResponseType,
            operationType);
    }

    private static EntryPointNode CreateEntryPointNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId,
        string? projectName = null)
    {
        var resolvedProj = !string.IsNullOrEmpty(projectName) ? projectName : GetProjectNameFromRelativePath(relativePath);
        if (!string.IsNullOrEmpty(resolvedProj) &&
            (resolvedProj.StartsWith("internal-service-", StringComparison.OrdinalIgnoreCase) ||
             resolvedProj.StartsWith("integration-service-", StringComparison.OrdinalIgnoreCase) ||
             resolvedProj.StartsWith("external-service-", StringComparison.OrdinalIgnoreCase)))
        {
            var stripped = WorkspaceConventions.NormalizeServiceName(resolvedProj);
            if (!string.IsNullOrEmpty(stripped)) resolvedProj = stripped;
        }
        if (string.IsNullOrEmpty(resolvedProj)) resolvedProj = "default";

        var entryType = "grpc";
        var cleanName = name;

        if (name.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            entryType = "queue-listener";
            cleanName = name[3..];
        }
        else if (name.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            entryType = "queue-listener";
            cleanName = name[6..];
        }
        else if (name.Contains(':'))
        {
            var idx = name.IndexOf(':');
            entryType = name[..idx];
            cleanName = name[(idx + 1)..];
        }

        var projPart = (!string.IsNullOrEmpty(resolvedProj) && resolvedProj != "default") ? $"{resolvedProj}:" : "";
        var entryPointId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:{projPart}{entryType}:{cleanName}";

        var ext = new Dictionary<string, string>
        {
            { "file_path", relativePath },
            { "start_line", (node.StartPosition.Row + 1).ToString() },
            { "start_col", (node.StartPosition.Column + 1).ToString() }
        };
        return new EntryPointNode(entryPointId, cleanName, relativePath, entryType, ext);
    }

    private static ExternalServiceNode CreateExternalServiceNode(
        string name,
        TreeSitter.Node node,
        string relativePath,
        string workspaceId,
        string? parentScopeId = null)
    {
        var cleanName = name?.Trim('"', '\'', '`', ' ', ';') ?? "";
        if (!WorkspaceConventions.IsTestFilePath(relativePath) &&
            cleanName.StartsWith("graphql:", StringComparison.OrdinalIgnoreCase))
        {
            var gqlRem = cleanName["graphql:".Length..].Trim();
            var gqlOpType = "Query";
            var gqlField = gqlRem;
            var colonIdx = gqlRem.IndexOf(':');
            if (colonIdx > 0)
            {
                var maybeOp = gqlRem[..colonIdx].ToUpperInvariant();
                if (maybeOp is "QUERY" or "MUTATION" or "SUBSCRIPTION")
                {
                    gqlOpType = maybeOp switch
                    {
                        "MUTATION" => "Mutation",
                        "SUBSCRIPTION" => "Subscription",
                        _ => "Query"
                    };
                    gqlField = gqlRem[(colonIdx + 1)..].Trim();
                }
            }

            if (!string.IsNullOrEmpty(gqlField))
            {
                var gqlPath = "/" + gqlField.TrimStart('/');
                var scopeKey = !string.IsNullOrEmpty(parentScopeId) ? parentScopeId : relativePath;
                var gqlExtId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:graphql:{gqlOpType}:{gqlField}:{scopeKey}";
                var gqlExt = new Dictionary<string, string>
                {
                    ["file_path"] = relativePath,
                    ["start_line"] = (node.StartPosition.Row + 1).ToString(),
                    ["start_col"] = (node.StartPosition.Column + 1).ToString(),
                    ["graphql_field"] = gqlField,
                    ["operation_type"] = gqlOpType,
                    ["is_internal"] = "true",
                    ["url"] = gqlPath
                };
                if (!string.IsNullOrEmpty(parentScopeId))
                {
                    gqlExt["caller_symbol_id"] = parentScopeId;
                }
                return new ExternalServiceNode(gqlExtId, gqlField, "graphql", "graphql", gqlPath, gqlExt);
            }
        }

        // Strip query string if present (? or &)
        var qEarly = cleanName.IndexOf('?');
        if (qEarly >= 0) cleanName = cleanName[..qEarly];
        var ampEarly = cleanName.IndexOf('&');
        if (ampEarly >= 0) cleanName = cleanName[..ampEarly];

        // Normalize wildcard-relative path like */api/v1/... to /api/v1/...
        if (cleanName.StartsWith("*/"))
        {
            cleanName = cleanName[1..];
        }

        var stripped = cleanName.Trim('*', '?', '&', '=', '/', ' ');
        if (string.IsNullOrWhiteSpace(stripped))
        {
            cleanName = "unknown-service";
        }

        if (WorkspaceConventions.IsTestFilePath(relativePath) ||
            string.IsNullOrWhiteSpace(cleanName) ||
            cleanName == "unknown-service" ||
            cleanName.StartsWith('*') ||
            cleanName.StartsWith('?') ||
            cleanName.StartsWith('&') ||
            cleanName.StartsWith('=') ||
            cleanName.Contains("api-version=") ||
            cleanName.Length > 256 ||
            cleanName.Contains('\n') ||
            cleanName.Contains('\r') ||
            (!cleanName.StartsWith("http", StringComparison.OrdinalIgnoreCase) && (cleanName.Contains('{') || cleanName.Contains('}'))) ||
            cleanName.Contains('<') ||
            cleanName.Contains('>') ||
            cleanName.Contains('+') ||
            cleanName.Contains('(') ||
            cleanName.Contains(')') ||
            cleanName.Contains('"') ||
            cleanName.Contains('\'') ||
            cleanName.StartsWith('$') ||
            cleanName.Contains("=>") ||
            cleanName.Contains(';') ||
            int.TryParse(cleanName, out _))
        {
            cleanName = "unknown-service";
        }

        var protocol = "http";
        var domainOrService = cleanName;
        var hasExplicitScheme = domainOrService.Contains("://") ||
                                domainOrService.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
                                domainOrService.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ||
                                domainOrService.StartsWith("ws:", StringComparison.OrdinalIgnoreCase) ||
                                domainOrService.StartsWith("wss:", StringComparison.OrdinalIgnoreCase) ||
                                domainOrService.StartsWith("grpc:", StringComparison.OrdinalIgnoreCase);

        if (domainOrService.Contains("://"))
        {
            var pIdx = domainOrService.IndexOf("://");
            protocol = domainOrService[..pIdx];
            domainOrService = domainOrService[(pIdx + 3)..];
        }
        else if (domainOrService.StartsWith("ws:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "ws";
            domainOrService = domainOrService[3..];
        }
        else if (domainOrService.StartsWith("http:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "http";
            domainOrService = domainOrService[5..];
        }
        else if (domainOrService.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
        {
            protocol = "https";
            domainOrService = domainOrService[6..];
        }
        else if (domainOrService.Contains(':'))
        {
            var idx = domainOrService.IndexOf(':');
            var candidateProto = domainOrService[..idx].ToLowerInvariant();
            if (candidateProto is "http" or "https" or "ws" or "wss" or "grpc")
            {
                hasExplicitScheme = true;
                protocol = candidateProto;
                domainOrService = domainOrService[(idx + 1)..];
            }
        }

        var hadExplicitPath = domainOrService.Contains('/');
        var path = "/";
        var slashIdx = domainOrService.IndexOf('/');
        if (slashIdx > 0)
        {
            var firstSegment = domainOrService[..slashIdx].ToLowerInvariant();
            if (firstSegment is "api" or "rest" or "v1" or "v2" or "v3" or "v4" or "v5" or "graphql")
            {
                domainOrService = "/" + domainOrService;
                slashIdx = 0;
            }
            else
            {
                path = domainOrService[slashIdx..];
                domainOrService = domainOrService[..slashIdx];
            }
        }

        if (slashIdx == 0)
        {
            var qIdx0 = domainOrService.IndexOf('?');
            if (qIdx0 >= 0) domainOrService = domainOrService[..qIdx0];
            var hIdx0 = domainOrService.IndexOf('#');
            if (hIdx0 >= 0) domainOrService = domainOrService[..hIdx0];

            path = domainOrService;
            var segments = domainOrService.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            string? candidate = null;
            for (var i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];
                if (seg.Equals("api", StringComparison.OrdinalIgnoreCase)) continue;
                if (seg.StartsWith("v", StringComparison.OrdinalIgnoreCase) && seg.Length <= 6 && seg.Skip(1).All(c => char.IsDigit(c) || c == '.')) continue;
                if (seg.Equals("assets", StringComparison.OrdinalIgnoreCase) || seg.Equals("static", StringComparison.OrdinalIgnoreCase) || seg.Equals("public", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = null;
                    break;
                }
                if ((seg.Equals("gateway", StringComparison.OrdinalIgnoreCase) || seg.Equals("gw", StringComparison.OrdinalIgnoreCase)) && i + 1 < segments.Length)
                {
                    continue;
                }
                candidate = seg;
                break;
            }

            if (!string.IsNullOrWhiteSpace(candidate) && candidate.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            {
                domainOrService = candidate;
            }
            else
            {
                domainOrService = "unknown-service";
            }
        }

        var qIdx = path.IndexOf('?');
        if (qIdx >= 0) path = path[..qIdx];
        var hIdx = path.IndexOf('#');
        if (hIdx >= 0) path = path[..hIdx];
        if (string.IsNullOrEmpty(path)) path = "/";

        var colonPortIdx = domainOrService.LastIndexOf(':');
        if (colonPortIdx > 0 && int.TryParse(domainOrService[(colonPortIdx + 1)..], out _))
        {
            domainOrService = domainOrService[..colonPortIdx];
        }

        var qIdxDomain = domainOrService.IndexOf('?');
        if (qIdxDomain >= 0) domainOrService = domainOrService[..qIdxDomain];
        var ampIdxDomain = domainOrService.IndexOf('&');
        if (ampIdxDomain >= 0) domainOrService = domainOrService[..ampIdxDomain];
        var eqIdxDomain = domainOrService.IndexOf('=');
        if (eqIdxDomain >= 0) domainOrService = domainOrService[..eqIdxDomain];

        if (domainOrService.StartsWith('*') ||
            domainOrService.StartsWith('?') ||
            domainOrService.StartsWith('&') ||
            domainOrService.StartsWith('=') ||
            domainOrService.Contains('&') ||
            domainOrService.Contains('?') ||
            domainOrService.Contains('=') ||
            domainOrService.Contains('*') ||
            domainOrService.Contains('{') ||
            domainOrService.Contains('}') ||
            domainOrService.Contains(' ') ||
            string.IsNullOrWhiteSpace(domainOrService.Trim('*', '?', '&', '=', '/', ' ')))
        {
            domainOrService = "unknown-service";
        }
        else if (domainOrService.Contains('.'))
        {
            if (domainOrService.Contains(' ') || domainOrService.Any(c => c is '"' or '\'' or '{' or '}'))
            {
                domainOrService = "unknown-service";
            }
        }
        else if (domainOrService != "*" &&
                 protocol != "ws" && protocol != "wss" && protocol != "grpc")
        {
            if (string.IsNullOrWhiteSpace(domainOrService) ||
                domainOrService.Contains(' ') ||
                !Regex.IsMatch(domainOrService, @"^[a-zA-Z0-9_\-]+$"))
            {
                domainOrService = "unknown-service";
            }
        }

        var normalizedDomain = WorkspaceConventions.NormalizeServiceName(domainOrService);
        if (!string.IsNullOrEmpty(normalizedDomain) && !domainOrService.Contains('.'))
        {
            domainOrService = normalizedDomain;
        }

        var isGarbage = WorkspaceConventions.IsGarbageExternalService(domainOrService) ||
                        WorkspaceConventions.IsGenericUrlStemOrVariable(domainOrService);
        if (isGarbage)
        {
            domainOrService = "unknown-service";
        }

        var serviceScopeSuffix = domainOrService == "unknown-service"
            ? $":{ConstantRegistry.ExtractProjectName(relativePath) ?? "generic"}"
            : "";
        var extServiceId = $"{workspaceId}:{OntologyConstants.IdPrefixes.ExternalService}:{protocol}:{domainOrService}{serviceScopeSuffix}";

        var ext = new Dictionary<string, string>
        {
            { "file_path", relativePath },
            { "start_line", (node.StartPosition.Row + 1).ToString() },
            { "start_col", (node.StartPosition.Column + 1).ToString() }
        };
        if (!string.IsNullOrEmpty(parentScopeId))
        {
            ext["caller_symbol_id"] = parentScopeId;
        }

        if (slashIdx == 0 || (isGarbage && path.StartsWith('/') && path != "/"))
        {
            ext["is_relative_path"] = "true";
            ext["inferred_from_path"] = "true";
            ext["is_internal"] = "true";
            ext["is_external"] = "false";
            ext["url"] = path;
        }
        else if (domainOrService is "localhost" or "127.0.0.1" || domainOrService.StartsWith("localhost:") || domainOrService.StartsWith("127.0.0.1:"))
        {
            ext["is_internal"] = "true";
            ext["is_external"] = "false";
        }
        else if (isGarbage || domainOrService == "unknown-service" ||
                 domainOrService.StartsWith('*') || domainOrService.StartsWith('?') || domainOrService.StartsWith('&') || domainOrService.StartsWith('='))
        {
            ext["is_external"] = "false";
        }
        else if (!hasExplicitScheme && !domainOrService.Contains('.') && !hadExplicitPath)
        {
            ext["is_external"] = "false";
            ext["inferred_from_identifier"] = "true";
        }
        else
        {
            ext["is_external"] = "true";
        }
        return new ExternalServiceNode(extServiceId, domainOrService, protocol, domainOrService, path, ext);
    }

    private static bool IsRouteOrConfigCandidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var p = path.ToLowerInvariant();
        return (p.EndsWith(".ts") || p.EndsWith(".js") || p.EndsWith(".cs") || p.EndsWith(".go") || p.EndsWith(".py") || p.EndsWith(".json")) &&
               (p.Contains("route") || p.Contains("const") || p.Contains("config") || p.Contains("api") || p.Contains("endpoint") || p.Contains("url") || p.Contains("env") ||
                p.Contains("client") || p.Contains("service") || p.Contains("importer") || p.Contains("catalog") || p.Contains("gateway") || p.Contains("http") ||
                p.Contains("main.") || p.Contains("app.") || p.Contains("bootstrap.") || p.Contains("index.") || p.Contains("program.") || p.Contains("startup."));
    }

    private static string GetProjectNameFromRelativePath(string relativePath)
    {
        var cleanPath = relativePath.Replace('\\', '/').Trim('/');
        var parts = cleanPath.Split('/');
        if (parts.Length == 0) return "default";

        if (parts.Length >= 2 && (parts[0] is "Core" or "Parsers" or "Tests"))
        {
            return parts[1];
        }

        return parts[0];
    }
}
