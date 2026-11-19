MATCH (cc:CatchClause)
WHERE cc.project_name = $projectName
  AND cc.is_empty = true
  AND (cc.is_test IS NULL OR cc.is_test = false)
RETURN cc.name AS name,
       'CatchClause' AS type,
       coalesce(cc.file_path, '') AS filePath,
       'empty_catch' AS anomalyType,
       coalesce(cc.line, 0) AS metricValue
ORDER BY filePath, metricValue
LIMIT 25

