MATCH (target) WHERE target.name = $target 
   OR target.symbol = $target 
   OR target.id = $target 
   OR target.symbol ENDS WITH (':' + $target)
   OR target.symbol ENDS WITH ('.' + $target)
   OR ((target:ExternalService OR target:Project OR target:Topic OR target:Table) AND replace(replace(toLower(target.name), '-', ''), '_', '') CONTAINS replace(replace(toLower($target), '-', ''), '_', ''))
MATCH (caller)-[r:CALLS|SERVICE_CALL|CALLS_ENDPOINT|USES_TYPE|USES_DB|QUERIES_DB|PUBLISHES_TO|SUBSCRIBES_TO|TRIGGERS|IMPLEMENTS|INHERITS_FROM]->(target)
WHERE $includeTests OR NOT (caller.is_test = true OR caller.file_path CONTAINS 'test' OR caller.file_path CONTAINS 'Test')
OPTIONAL MATCH (caller)-[:DECLARED_IN]->(f:File)
RETURN 
  target.name AS targetName,
  labels(target)[0] AS targetKind,
  caller.name AS callerName,
  labels(caller)[0] AS callerKind,
  type(r) AS relKind,
  COALESCE(caller.project_name, '') AS projectName,
  CASE 
    WHEN COALESCE(caller.path, caller.file_path, f.path) IS NOT NULL AND caller.start_line IS NOT NULL 
    THEN COALESCE(caller.path, caller.file_path, f.path) + ':' + toString(caller.start_line)
    WHEN COALESCE(caller.path, caller.file_path, f.path) IS NOT NULL 
    THEN COALESCE(caller.path, caller.file_path, f.path)
    ELSE '' 
  END AS location
ORDER BY targetName, callerName
