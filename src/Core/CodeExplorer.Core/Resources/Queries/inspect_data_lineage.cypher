MATCH (t:Table {name: $tableName})
OPTIONAL MATCH (q:Query)-[:DEPENDS_ON]->(t)
OPTIONAL MATCH (t)-[:QUERIED_BY]->(func:Function)
OPTIONAL MATCH (caller)-[:CALLS]->(func)
OPTIONAL MATCH (entity:Type)-[:PERSISTED_IN]->(t)
RETURN t.name AS tableName,
       coalesce(q.name, '') AS queryName,
       coalesce(q.query_text, '') AS queryText,
       coalesce(q.path, '') AS filePath,
       collect(DISTINCT coalesce(entity.name, func.name)) AS parentName,
       CASE WHEN entity IS NOT NULL THEN 'Entity' ELSE 'Function' END AS parentType,
       collect(DISTINCT caller.name) AS callingSymbols
