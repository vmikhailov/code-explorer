MATCH (target) WHERE target.name = $target 
   OR target.symbol = $target 
   OR target.id = $target 
   OR target.symbol CONTAINS (':' + $target + ':')
   OR target.symbol ENDS WITH (':' + $target)
   OR target.symbol ENDS WITH ('.' + $target)
   OR ((target:ExternalService OR target:Project OR target:Topic OR target:Table) AND replace(replace(toLower(target.name), '-', ''), '_', '') CONTAINS replace(replace(toLower($target), '-', ''), '_', ''))
MATCH (caller)-[r:CALLS|SERVICE_CALL|CALLS_ENDPOINT|USES_TYPE|USES_DB|QUERIES_DB|PUBLISHES_TO|SUBSCRIBES_TO|TRIGGERS|IMPLEMENTS|INHERITS_FROM]->(target)
WHERE $includeTests OR NOT (COALESCE(caller.is_test, false) = true OR COALESCE(caller.project_name, '') CONTAINS '.Test' OR COALESCE(caller.path, caller.file_path, '') CONTAINS '/tests/' OR COALESCE(caller.path, caller.file_path, '') CONTAINS '/test/' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH 'Tests.cs' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '.spec.ts' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '.test.ts' OR COALESCE(caller.path, caller.file_path, '') ENDS WITH '_test.go')
OPTIONAL MATCH (callerOwner:Type)-[:HAS_METHOD]->(caller)
OPTIONAL MATCH (caller)-[:DECLARED_IN]->(f:File)
RETURN 
  target.name AS targetName,
  labels(target)[0] AS targetKind,
  CASE WHEN callerOwner IS NOT NULL THEN callerOwner.name + '.' + caller.name ELSE caller.name END AS callerName,
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
