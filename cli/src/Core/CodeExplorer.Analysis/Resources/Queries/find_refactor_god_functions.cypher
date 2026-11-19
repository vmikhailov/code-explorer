MATCH (p:Project {name: $projectName})<-[:BELONGS_TO]-(ps:ProjectSyntax)-[:CONTAINS]->(fn:Function)
WHERE (fn.is_test IS NULL OR fn.is_test = false)
  AND fn.name <> '<module_init>'
  AND NOT fn.name STARTS WITH 'Jest:'
  AND fn.start_line IS NOT NULL
  AND fn.end_line IS NOT NULL
OPTIONAL MATCH (fn)-[:DECLARED_IN]->(f:File)
WITH fn, f, (fn.end_line - fn.start_line + 1) AS lineCount
WHERE lineCount > 150
RETURN fn.name AS name,
       'Function' AS type,
       coalesce(f.path, fn.file_path, '') AS filePath,
       'god_function' AS anomalyType,
       lineCount AS metricValue,
       fn.symbol AS symbol
ORDER BY lineCount DESC
LIMIT 20

