MATCH (p:Project {name: $projectName})-[:CONTAINS*1..]->(f:File) {prefixClause}
MATCH (c:Type {kind: 'class'})-[:DECLARED_IN]->(f)
MATCH (c)-[:HAS_METHOD|HAS_MEMBER]->(member)
WITH c, f, count(member) AS memberCount WHERE memberCount > 15
RETURN c.name AS name, 'Class' AS type, f.path AS filePath, 'god_object' AS anomalyType, memberCount AS metricValue, c.symbol AS symbol
ORDER BY memberCount DESC LIMIT 20
