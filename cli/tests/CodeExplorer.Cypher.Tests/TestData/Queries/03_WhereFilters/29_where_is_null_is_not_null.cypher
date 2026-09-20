MATCH (f:File)
OPTIONAL MATCH (f)-[:DEFINES]->(t:Type)
WHERE t.name IS NULL
RETURN f.name AS fileName

