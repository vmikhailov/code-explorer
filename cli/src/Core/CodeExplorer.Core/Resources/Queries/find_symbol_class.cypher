MATCH (n:Type {kind: 'class'}) WHERE n.name CONTAINS $name
OPTIONAL MATCH (n)-[:DECLARED_IN]->(f:File)
OPTIONAL MATCH (w:Workspace)
RETURN 'Class' AS type, n.name AS name, n.symbol AS fullName,
CASE WHEN f IS NOT NULL AND w IS NOT NULL
     THEN w.path + '/' + f.path
     ELSE coalesce(n.file_path, f.path) END AS filePath,
coalesce(n.start_line, 0) AS startLine, coalesce(n.end_line, 0) AS endLine LIMIT 10
