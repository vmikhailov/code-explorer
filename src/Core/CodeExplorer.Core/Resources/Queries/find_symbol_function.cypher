MATCH (n:Function) WHERE n.name CONTAINS $name
OPTIONAL MATCH (n)-[:DECLARED_IN]->(f:File)
OPTIONAL MATCH (w:Workspace)
RETURN 'Function' AS type, n.name AS name, n.symbol AS fullName,
CASE WHEN f IS NOT NULL AND w IS NOT NULL
     THEN w.path + '/' + f.path
     ELSE coalesce(n.file_path, f.path) END AS filePath LIMIT 10
