MATCH (w:Workspace)-[:CONTAINS]->(f:File)
RETURN w.name + '/' + f.name AS fullPath

