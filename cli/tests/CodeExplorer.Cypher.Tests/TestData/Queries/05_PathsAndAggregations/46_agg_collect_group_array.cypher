MATCH (t:Type)-[:HAS_METHOD]->(m:Function)
RETURN t.name AS typeName, collect(m.name) AS methods

