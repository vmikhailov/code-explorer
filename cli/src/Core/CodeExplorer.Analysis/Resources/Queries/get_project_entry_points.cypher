MATCH (ep)-[:DECLARED_IN]->(p:Project)
WHERE (ep:Endpoint OR ep:EntryPoint)
  AND ($projectName IS NULL OR $projectName = '' OR p.name = $projectName)
OPTIONAL MATCH (ep)-[:TRIGGERS|IMPLEMENTED_BY]->(func:Function)
OPTIONAL MATCH (ep)-[:EXPOSED_BY]->(class:Type)
OPTIONAL MATCH (func)-[:DECLARED_IN]->(f:File)
RETURN coalesce(func.name, ep.name) AS entryPoint,
       coalesce(func.symbol, ep.symbol) AS symbol,
       coalesce(class.name, '') AS className,
       coalesce(f.path, func.file_path, ep.file_path, ep.path, '') AS filePath,
       coalesce(func.start_line, ep.start_line, 0) AS startLine,
       coalesce(ep.route, ep.route_template, '') AS route,
       coalesce(ep.http_method, '') AS httpMethod,
       coalesce(ep.protocol, 'REST') AS protocol,
       coalesce(ep.request_type, '') AS requestType,
       coalesce(ep.response_type, '') AS responseType,
       coalesce(ep.operation_type, '') AS operationType,
       coalesce(ep.is_anonymous, false) AS isAnonymous,
       coalesce(ep.required_roles, '') AS requiredRoles
