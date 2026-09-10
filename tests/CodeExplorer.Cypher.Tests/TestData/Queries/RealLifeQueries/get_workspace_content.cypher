MATCH (r:Root {path: $workspacePath})-[:CONTAINS*0..]->(n)
WHERE $type IS NULL OR $type = '' OR any(lbl IN labels(n) WHERE lbl = $type)
RETURN n LIMIT 1000
