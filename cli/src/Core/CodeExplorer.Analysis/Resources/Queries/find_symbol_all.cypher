MATCH (n) WHERE (n:Function OR n:Type OR n:CloudResource) AND n.name IS NOT NULL AND n.name CONTAINS $name
OPTIONAL MATCH (n)-[:DECLARED_IN]->(f:File)
RETURN CASE WHEN n:Type THEN (CASE WHEN n.kind = 'class' THEN 'Class' WHEN n.kind = 'interface' THEN 'Interface' ELSE n.kind END) ELSE labels(n)[0] END AS type, n.name AS name, coalesce(n.symbol, n.name) AS fullName,
coalesce(f.path, n.file_path, n.path) AS filePath,
coalesce(n.start_line, n.line, 0) AS startLine, coalesce(n.end_line, n.line_end, 0) AS endLine LIMIT 10
