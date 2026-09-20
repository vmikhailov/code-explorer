MATCH (p:Project {name: $projectName})<-[:BELONGS_TO]-(ps:ProjectSyntax)-[:CONTAINS]->(item)
WHERE item:Function OR item:Type
OPTIONAL MATCH (item)-[:DECLARED_IN]->(f:File)
OPTIONAL MATCH (caller)-[:CALLS|USES_TYPE]->(item)
WITH f, item, caller
WHERE caller IS NULL
RETURN item.name AS name,
       CASE WHEN item:Type THEN (CASE WHEN item.kind = 'class' THEN 'Class' WHEN item.kind = 'interface' THEN 'Interface' ELSE item.kind END) ELSE labels(item)[0] END AS type,
       coalesce(f.path, '') AS filePath,
       'dead_code' AS anomalyType,
       item.symbol AS symbol
LIMIT 50
