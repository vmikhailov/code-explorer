MATCH (p:Project {name: $projectName})<-[:BELONGS_TO]-(psem:ProjectSemantic)-[:CONTAINS]->(ep)
WHERE ep:Endpoint OR ep:EntryPoint
OPTIONAL MATCH (ep)-[:TRIGGERS]->(func:Function)
OPTIONAL MATCH (ep)-[:EXPOSED_BY]->(class:Type)
OPTIONAL MATCH (func)-[:DECLARED_IN]->(f:File)
RETURN coalesce(func.name, ep.name) AS entryPoint,
       coalesce(func.symbol, ep.symbol) AS symbol,
       coalesce(class.name, '') AS className,
       coalesce(f.path, '') AS filePath,
       coalesce(func.start_line, 0) AS startLine,
       coalesce(ep.route, '') AS route,
       coalesce(ep.http_method, '') AS httpMethod
