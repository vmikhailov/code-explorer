MATCH (w:Workspace)-[:CONTAINS]->(f:File)
RETURN w.name AS wsName, count(w) AS totalWorkspaces, count(f.name) AS namedFiles

