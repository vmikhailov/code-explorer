MATCH (p:Project {name: $projectName})-[:CONTAINS*1..]->(f:File) {prefixClause}
MATCH (item)-[:DECLARED_IN]->(f) WHERE item:Function OR item:Type
OPTIONAL MATCH (caller)-[:CALLS|USES_TYPE]->(item) WITH f, item, caller
WHERE caller IS NULL
RETURN item.name AS name, CASE WHEN item:Type THEN (CASE WHEN item.kind = 'class' THEN 'Class' WHEN item.kind = 'interface' THEN 'Interface' ELSE item.kind END) ELSE labels(item)[0] END AS type, f.path AS filePath, 'dead_code' AS anomalyType, item.symbol AS symbol LIMIT 50
