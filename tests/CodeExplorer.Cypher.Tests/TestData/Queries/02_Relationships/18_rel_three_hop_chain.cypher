MATCH (w:Workspace)-[:CONTAINS]->(f:File)-[:DEFINES]->(t:Type)-[:HAS_METHOD]->(m:Function)
RETURN w.name AS wsName, m.name AS methodName

