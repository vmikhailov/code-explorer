using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.Kotlin;

public partial class KotlinFileParser
{
    private const string ProtocolGrpc = "gRPC";
    private const string OperationRpc = "RPC";

    private static void ParseFunctionsAndEndpoints(
        string content,
        string commentMasked,
        string masked,
        int[] lineOffsets,
        List<ClassScope> classScopes,
        FileNode fileNode,
        string workspaceId,
        string relativePath,
        List<SyntacticSymbol> semanticSymbols)
    {
        var funMatches = FunDeclRegex.Matches(masked);

        foreach (Match fm in funMatches)
        {
            var funName = fm.Groups["name"].Value.Trim();
            var (funLine, funCol) = GetLineAndColumn(lineOffsets, fm.Index);

            ClassScope? enclosingClass = null;
            var bestSpan = int.MaxValue;
            foreach (var cs in classScopes)
            {
                if (cs.OpenBraceIndex >= 0 && cs.CloseBraceIndex >= 0 &&
                    fm.Index >= cs.OpenBraceIndex && fm.Index <= cs.CloseBraceIndex)
                {
                    var span = cs.CloseBraceIndex - cs.OpenBraceIndex;
                    if (span < bestSpan)
                    {
                        bestSpan = span;
                        enclosingClass = cs;
                    }
                }
            }

            var funId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Function:{funName}";
            var funcNode = new FunctionNode(
                funId,
                funName,
                funName,
                relativePath,
                relativePath,
                funLine,
                funLine,
                funCol,
                funCol
            );

            if (enclosingClass != null)
            {
                enclosingClass.Node.Children.Add(funcNode);
            }
            else
            {
                fileNode.Children.Add(funcNode);
            }

            if (enclosingClass?.IsGrpcService == true &&
                !funName.StartsWith('<') && funName != "equals" && funName != "hashCode" && funName != "toString")
            {
                var serviceName = enclosingClass.Name.Replace("GrpcService", string.Empty);
                var fullRoute = $"/{serviceName}/{funName}";
                var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{OperationRpc}:{fullRoute}";
                var epName = $"{OperationRpc} {fullRoute}";

                var epNode = new EndpointNode(
                    epId,
                    epName,
                    relativePath,
                    OperationRpc,
                    fullRoute,
                    ProtocolGrpc
                )
                {
                    FilePath = relativePath,
                    StartLine = funLine,
                    EndLine = funLine,
                    Line = funLine,
                    LineEnd = funLine,
                    Operation = OperationRpc,
                    EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                };
                epNode.CustomProperties ??= new();
                epNode.CustomProperties["path"] = fullRoute;
                epNode.CustomProperties["route"] = fullRoute;
                epNode.CustomProperties["route_template"] = fullRoute;
                epNode.CustomProperties["method"] = OperationRpc;
                epNode.CustomProperties["http_method"] = OperationRpc;
                epNode.CustomProperties["file_path"] = relativePath;
                fileNode.Children.Add(epNode);

                fileNode.References.Add(new Reference(epId, funcNode.Id, OntologyConstants.Relationships.ExposedBy, funLine, funCol));
            }

            var annotations = ExtractPrecedingAnnotations(commentMasked, masked, fm.Index);

            var fnSyntaxNode = new FunctionDeclSyntax
            {
                Name = funName,
                Span = new TextSpan(fm.Index, fm.Index + fm.Length, funLine - 1, funCol - 1, funLine - 1, funCol - 1)
            };

            foreach (var ann in annotations)
            {
                if (ann.Name.Equals("Path", StringComparison.OrdinalIgnoreCase) &&
                    annotations.Any(a => a != ann && a.Name is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" or "HEAD" or "OPTIONS"))
                {
                    continue;
                }

                var httpMethod = ann.Name switch
                {
                    "GetMapping" or "GET" => "GET",
                    "PostMapping" or "POST" => "POST",
                    "PutMapping" or "PUT" => "PUT",
                    "DeleteMapping" or "DELETE" => "DELETE",
                    "PatchMapping" or "PATCH" => "PATCH",
                    "HEAD" => "HEAD",
                    "OPTIONS" => "OPTIONS",
                    "Path" => "ALL",
                    "RequestMapping" => DetermineHttpMethodFromRequestMapping(ann.Args),
                    _ => null
                };

                if (httpMethod != null)
                {
                    var paths = ExtractRoutePathsFromAnnotation(ann.Args);
                    if (paths.Count == 0 || (paths.Count == 1 && string.IsNullOrEmpty(paths[0])))
                    {
                        var siblingPathAnn = annotations.FirstOrDefault(a => a.Name.Equals("Path", StringComparison.OrdinalIgnoreCase));
                        if (siblingPathAnn != null)
                        {
                            paths = ExtractRoutePathsFromAnnotation(siblingPathAnn.Args);
                        }
                    }

                    foreach (var rawPath in paths)
                    {
                        var fullRoute = CombineRoutes(enclosingClass?.RoutePrefix, rawPath);
                        var epId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Endpoint}:{httpMethod}:{fullRoute}";
                        var epName = $"{httpMethod} {fullRoute}";

                        var epNode = new EndpointNode(
                            epId,
                            epName,
                            relativePath,
                            httpMethod,
                            fullRoute,
                            ProtocolRest
                        )
                        {
                            FilePath = relativePath,
                            StartLine = funLine,
                            EndLine = funLine,
                            Line = funLine,
                            LineEnd = funLine,
                            Operation = httpMethod,
                            EvidenceKind = OntologyConstants.EvidenceKinds.Endpoint
                        };
                        epNode.CustomProperties ??= new();
                        epNode.CustomProperties["path"] = fullRoute;
                        epNode.CustomProperties["route"] = fullRoute;
                        epNode.CustomProperties["route_template"] = fullRoute;
                        epNode.CustomProperties["method"] = httpMethod;
                        epNode.CustomProperties["http_method"] = httpMethod;
                        epNode.CustomProperties["file_path"] = relativePath;
                        fileNode.Children.Add(epNode);

                        fileNode.References.Add(new Reference(epId, funcNode.Id, OntologyConstants.Relationships.ExposedBy, funLine, funCol));
                    }
                }

                if (ann.Name.Equals("Query", StringComparison.OrdinalIgnoreCase))
                {
                    var sqlCandidate = ExtractQuerySql(ann.Args);
                    if (!string.IsNullOrWhiteSpace(sqlCandidate))
                    {
                        var cleanSql = NestedSqlParser.CleanQueryText(sqlCandidate);
                        var qId = $"{workspaceId}:{OntologyConstants.IdPrefixes.Query}:{relativePath}:{funLine}:{funCol}";
                        var qName = $"JPA Query: {cleanSql}";

                        var queryNode = new QueryNode(qId, qName, cleanSql, relativePath, funLine, funLine)
                        {
                            FilePath = relativePath,
                            Line = funLine,
                            LineEnd = funLine,
                            Operation = "SELECT",
                            EvidenceKind = OntologyConstants.EvidenceKinds.Query
                        };
                        queryNode.CustomProperties ??= new();
                        queryNode.CustomProperties["file_path"] = relativePath;
                        fileNode.Children.Add(queryNode);

                        fileNode.References.Add(new Reference(qId, funcNode.Id, OntologyConstants.Relationships.AccessedBy, funLine, funCol));
                    }
                }
            }
        }
    }

    private static string DetermineHttpMethodFromRequestMapping(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return "ALL";
        if (args.Contains("RequestMethod.POST", StringComparison.OrdinalIgnoreCase)) return "POST";
        if (args.Contains("RequestMethod.PUT", StringComparison.OrdinalIgnoreCase)) return "PUT";
        if (args.Contains("RequestMethod.DELETE", StringComparison.OrdinalIgnoreCase)) return "DELETE";
        if (args.Contains("RequestMethod.PATCH", StringComparison.OrdinalIgnoreCase)) return "PATCH";
        if (args.Contains("RequestMethod.HEAD", StringComparison.OrdinalIgnoreCase)) return "HEAD";
        if (args.Contains("RequestMethod.OPTIONS", StringComparison.OrdinalIgnoreCase)) return "OPTIONS";
        if (args.Contains("RequestMethod.GET", StringComparison.OrdinalIgnoreCase)) return "GET";
        return "ALL";
    }
}
