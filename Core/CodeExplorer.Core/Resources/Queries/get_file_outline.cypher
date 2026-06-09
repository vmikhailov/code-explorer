MATCH (f:File) WHERE (f.path ENDS WITH $filePath OR f.file_path = $filePath) AND f.id STARTS WITH $wsIdPrefix
OPTIONAL MATCH (f)-[:DEFINES|DECLARES*1..]->(child)
WHERE child:Type OR child:Function OR child:Member OR child:Query
RETURN child.name AS name, CASE WHEN child:Type THEN (CASE WHEN child.kind = 'class' THEN 'Class' WHEN child.kind = 'interface' THEN 'Interface' ELSE child.kind END) ELSE labels(child)[0] END AS type, child.start_line AS startLine, child.end_line AS endLine, child.symbol AS symbol
ORDER BY child.start_line
