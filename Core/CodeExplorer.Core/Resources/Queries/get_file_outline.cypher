MATCH (f:File) WHERE (f.path ENDS WITH $filePath OR f.file_path = $filePath) AND f.id STARTS WITH $wsIdPrefix
OPTIONAL MATCH (f)-[:DEFINES|DECLARES*1..]->(child)
WHERE child:Class OR child:Interface OR child:Function OR child:Variable OR child:Query
RETURN child.name AS name, labels(child)[0] AS type, child.start_line AS startLine, child.end_line AS endLine, child.symbol AS symbol
ORDER BY child.start_line
