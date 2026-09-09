MATCH (p:Project)
RETURN p.name AS project, [(p)-[:CONTAINS]->(f:File) | f.path] AS files
