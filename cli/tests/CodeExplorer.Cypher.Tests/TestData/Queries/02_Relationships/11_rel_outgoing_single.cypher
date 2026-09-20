MATCH (w:Workspace)-[:CONTAINS]->(f:File)
RETURN w.name AS wsName, f.name AS fileName

