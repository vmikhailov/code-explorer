MATCH (p:Project {name: $projectName})<-[:BELONGS_TO]-(ps:ProjectSyntax)-[:CONTAINS]->(c:Type {kind: 'class'})
OPTIONAL MATCH (c)-[:DECLARED_IN]->(f:File)
MATCH (c)-[:HAS_METHOD|HAS_MEMBER]->(member)
WITH c, f, count(member) AS memberCount WHERE memberCount > 15
RETURN c.name AS name, 'Class' AS type, coalesce(f.path, '') AS filePath, 'god_object' AS anomalyType, memberCount AS metricValue, c.symbol AS symbol
ORDER BY memberCount DESC LIMIT 20
