MATCH (f:File) WHERE f.path IS NOT NULL AND (f.path = $filePath OR f.path ENDS WITH ('/' + $filePath))
OPTIONAL MATCH (child)-[:DECLARED_IN]->(f)
WHERE child:Type OR child:Function OR child:Member OR child:Query OR child:CloudResource
WITH child WHERE child IS NOT NULL
RETURN child.name AS name,
       CASE WHEN child:Type THEN (CASE WHEN child.kind = 'class' THEN 'Class' WHEN child.kind = 'interface' THEN 'Interface' ELSE child.kind END) ELSE labels(child)[0] END AS type,
       coalesce(child.start_line, child.line, 0) AS startLine,
       coalesce(child.end_line, child.line_end, 0) AS endLine,
       coalesce(child.symbol, child.name) AS symbol
UNION
MATCH (f:File) WHERE f.path IS NOT NULL AND (f.path = $filePath OR f.path ENDS WITH ('/' + $filePath))
MATCH (owner:Type)-[:DECLARED_IN]->(f)
MATCH (owner)-[:HAS_METHOD|HAS_MEMBER]->(child)
WHERE child:Function OR child:Member
RETURN child.name AS name,
       labels(child)[0] AS type,
       coalesce(child.start_line, child.line, 0) AS startLine,
       coalesce(child.end_line, child.line_end, 0) AS endLine,
       coalesce(child.symbol, child.name) AS symbol
