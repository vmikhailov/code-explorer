MATCH (t:Table)
WHERE $tableName IS NULL OR $tableName = '' OR $tableName = '*' OR t.name = $tableName OR toLower(t.name) CONTAINS toLower($tableName)
OPTIONAL MATCH (q:Query)-[:READS_FROM|WRITES_TO|DEPENDS_ON]->(t)
OPTIONAL MATCH (q)-[:ACCESSED_BY]->(qFunc:Function)
OPTIONAL MATCH (rwFunc:Function)-[:READS_FROM|WRITES_TO]->(t)
OPTIONAL MATCH (t)-[:ACCESSED_BY|QUERIED_BY]->(legacyFunc:Function)
OPTIONAL MATCH (t)-[:IMPLEMENTED_BY]->(entity:Type)
RETURN t.name AS tableName,
       coalesce(q.name, '') AS queryName,
       coalesce(q.query_text, '') AS queryText,
       coalesce(q.file_path, q.path, '') AS filePath,
       collect(DISTINCT coalesce(entity.name, rwFunc.name, qFunc.name, legacyFunc.name, q.name)) AS parentName,
       CASE WHEN entity IS NOT NULL THEN 'Entity' ELSE 'Function' END AS parentType
LIMIT 50
