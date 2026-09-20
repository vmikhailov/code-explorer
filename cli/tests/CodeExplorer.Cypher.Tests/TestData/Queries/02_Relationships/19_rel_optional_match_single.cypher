MATCH (f:File)
OPTIONAL MATCH (f)-[:DEFINES]->(t:Type)
RETURN f.name AS fileName, t.name AS typeName

