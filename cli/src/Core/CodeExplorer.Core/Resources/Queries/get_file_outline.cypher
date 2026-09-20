MATCH (f:File) WHERE f.path = $filePath OR f.name = $filePath OR f.full_path = $filePath OR f.path ENDS WITH ('/' + $filePath)
OPTIONAL MATCH (child)-[:DECLARED_IN]->(f)
WHERE child:Type OR child:Function OR child:Member OR child:Query
RETURN child.name AS name,
       CASE WHEN child:Type THEN (CASE WHEN child.kind = 'class' THEN 'Class' WHEN child.kind = 'interface' THEN 'Interface' ELSE child.kind END) ELSE labels(child)[0] END AS type,
       coalesce(child.start_line, 0) AS startLine,
       coalesce(child.end_line, 0) AS endLine,
       coalesce(child.symbol, child.name) AS symbol
ORDER BY child.start_line
