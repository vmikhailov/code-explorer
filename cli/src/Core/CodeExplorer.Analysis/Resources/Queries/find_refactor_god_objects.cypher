MATCH (p:Project)<-[:BELONGS_TO]-(ps:ProjectSyntax)-[:CONTAINS]->(c:Type {kind: 'class'})
WHERE ($projectName IS NULL OR $projectName = '' OR p.name = $projectName)
  AND (c.is_test IS NULL OR c.is_test = false)
OPTIONAL MATCH (c)-[:DECLARED_IN]->(f:File)
WHERE f IS NULL OR coalesce(f.artifact_role, 'source') <> 'vendor'
MATCH (c)-[:HAS_METHOD|HAS_MEMBER]->(member)
WITH c, f, count(member) AS memberCount WHERE memberCount > 15
RETURN c.name AS name, 'Class' AS type, coalesce(f.path, c.file_path, '') AS filePath, 'god_object' AS anomalyType, memberCount AS metricValue, c.symbol AS symbol
ORDER BY memberCount DESC LIMIT 20
