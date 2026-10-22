MATCH (t:Table)
WHERE $tableName IS NULL OR $tableName = '' OR $tableName = '*' OR t.name = $tableName OR toLower(t.name) CONTAINS toLower($tableName)
OPTIONAL MATCH (q:Query)-[:DEPENDS_ON]->(t)
OPTIONAL MATCH (t)-[:ACCESSED_BY|QUERIED_BY]->(func:Function)
OPTIONAL MATCH (caller)-[:CALLS]->(func)
OPTIONAL MATCH (t)-[:IMPLEMENTED_BY]->(entity:Type)
RETURN t.name AS tableName,
       coalesce(q.name, '') AS queryName,
       coalesce(q.query_text, '') AS queryText,
       coalesce(q.path, '') AS filePath,
       collect(DISTINCT coalesce(entity.name, func.name)) AS parentName,
       CASE WHEN entity IS NOT NULL THEN 'Entity' ELSE 'Function' END AS parentType,
       collect(DISTINCT caller.name) AS callingSymbols
LIMIT 50
