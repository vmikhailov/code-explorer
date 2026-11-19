MATCH (cc:CatchClause)
WHERE ($projectName IS NULL OR $projectName = '' OR cc.project_name = $projectName)
  AND cc.is_empty = true
  AND (cc.is_test IS NULL OR cc.is_test = false)
OPTIONAL MATCH (f:File) WHERE f.path = cc.file_path
WITH cc, f
WHERE f IS NULL OR coalesce(f.artifact_role, 'source') <> 'vendor'
RETURN cc.name AS name,
       'CatchClause' AS type,
       coalesce(cc.file_path, '') AS filePath,
       'empty_catch' AS anomalyType,
       coalesce(cc.line, 0) AS metricValue
ORDER BY filePath, metricValue
LIMIT 25

