MATCH (t:Table {name: $tableName})
OPTIONAL MATCH (q:Query)-[:DEPENDS_ON]->(t)
OPTIONAL MATCH (t)-[:QUERIED_BY]->(func:Function)
OPTIONAL MATCH (caller)-[:CALLS]->(func)
RETURN t.name AS tableName,
       coalesce(q.name, '') AS queryName,
       coalesce(q.query_text, '') AS queryText,
       coalesce(q.path, '') AS filePath,
       collect(DISTINCT func.name) AS parentName,
       'Function' AS parentType,
       collect(DISTINCT caller.name) AS callingSymbols
