MATCH (t:Type)
OPTIONAL MATCH (t)-[:HAS_METHOD]->(m:Function)
OPTIONAL MATCH (t)-[:USES_TYPE]->(dep:Type)
RETURN t.name AS typeName, m.name AS methodName, dep.name AS depName

