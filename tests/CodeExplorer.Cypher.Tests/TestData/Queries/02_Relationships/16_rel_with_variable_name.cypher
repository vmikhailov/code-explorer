MATCH (f:File)-[r:DEFINES]->(t:Type)
RETURN f.name AS fileName, type(r) AS relType, t.name AS typeName

