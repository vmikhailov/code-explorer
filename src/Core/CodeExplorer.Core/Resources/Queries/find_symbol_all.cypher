MATCH (n) WHERE (n:Function OR n:Type) AND n.name CONTAINS $name
OPTIONAL MATCH (n)-[:DECLARED_IN]->(f:File)
OPTIONAL MATCH (w:Workspace)
RETURN CASE WHEN n:Type THEN (CASE WHEN n.kind = 'class' THEN 'Class' WHEN n.kind = 'interface' THEN 'Interface' ELSE n.kind END) ELSE labels(n)[0] END AS type, n.name AS name, n.symbol AS fullName,
CASE WHEN f IS NOT NULL AND w IS NOT NULL
     THEN w.path + '/' + f.path
     ELSE coalesce(n.file_path, f.path) END AS filePath,
coalesce(n.start_line, 0) AS startLine, coalesce(n.end_line, 0) AS endLine LIMIT 10
