MATCH (p:Project)
RETURN p.name AS project, [(p)-[:CONTAINS]->(f:File) WHERE f.name ENDS WITH '.cs' | f.path] AS csFiles
