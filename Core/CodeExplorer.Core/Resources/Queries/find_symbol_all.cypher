MATCH (n) WHERE (n:Function OR n:Type) AND n.name CONTAINS $name{prefixClause}
OPTIONAL MATCH (f:File)-[:DEFINES|DECLARES*1..]->(n)
OPTIONAL MATCH (w:Workspace)-[:CONTAINS*1..]->(f)
RETURN CASE WHEN n:Type THEN (CASE WHEN n.kind = 'class' THEN 'Class' WHEN n.kind = 'interface' THEN 'Interface' ELSE n.kind END) ELSE labels(n)[0] END AS type, n.name AS name, n.symbol AS fullName,
CASE WHEN f IS NOT NULL AND w IS NOT NULL
     THEN w.path + '/' + f.path
     ELSE n.file_path END AS filePath LIMIT 10
