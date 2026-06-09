MATCH (n) WHERE (n:Function OR n:Class OR n:Interface) AND n.name CONTAINS $name{prefixClause}
OPTIONAL MATCH (f:File)-[:DEFINES|DECLARES*1..]->(n)
OPTIONAL MATCH (w:Workspace)-[:CONTAINS*1..]->(f)
RETURN labels(n)[0] AS type, n.name AS name, n.symbol AS fullName,
CASE WHEN f IS NOT NULL AND w IS NOT NULL
     THEN w.path + '/' + f.path
     ELSE n.file_path END AS filePath LIMIT 10
