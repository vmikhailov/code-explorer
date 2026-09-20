MATCH (w:Workspace)-[:CONTAINS]->(f:File)-[:DEFINES]->(t:Type)
RETURN w.name AS wsName, f.name AS fileName, t.name AS typeName

