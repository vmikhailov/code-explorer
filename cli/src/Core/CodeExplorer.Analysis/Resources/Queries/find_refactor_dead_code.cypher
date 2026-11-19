MATCH (p:Project {name: $projectName})<-[:BELONGS_TO]-(ps:ProjectSyntax)-[:CONTAINS]->(item)
WHERE (item:Function OR item:Type)
  AND (item.is_test IS NULL OR item.is_test = false)
  AND item.name <> '<module_init>'
  AND NOT item.name STARTS WITH 'Jest:'
OPTIONAL MATCH (item)-[:DECLARED_IN]->(f:File)
OPTIONAL MATCH (caller)-[:CALLS|USES_TYPE]->(item)
WITH f, item, caller
WHERE caller IS NULL
  AND NOT coalesce(f.path, '') CONTAINS '.config.'
  AND NOT coalesce(f.path, '') CONTAINS 'jest.setup'
  AND NOT coalesce(f.path, '') CONTAINS 'jest.env'
  AND NOT coalesce(f.path, '') ENDS WITH '.d.ts'
RETURN item.name AS name,
       CASE WHEN item:Type THEN (CASE WHEN item.kind = 'class' THEN 'Class' WHEN item.kind = 'interface' THEN 'Interface' ELSE item.kind END) ELSE labels(item)[0] END AS type,
       coalesce(f.path, '') AS filePath,
       'dead_code' AS anomalyType
LIMIT 20
