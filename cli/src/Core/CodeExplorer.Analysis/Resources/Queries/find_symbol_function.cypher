MATCH (n:Function) WHERE n.name IS NOT NULL AND n.name CONTAINS $name
OPTIONAL MATCH (n)-[:DECLARED_IN]->(f:File)
RETURN 'Function' AS type, n.name AS name, n.symbol AS fullName,
coalesce(f.path, n.file_path, n.path) AS filePath,
coalesce(n.start_line, 0) AS startLine, coalesce(n.end_line, 0) AS endLine LIMIT 10
