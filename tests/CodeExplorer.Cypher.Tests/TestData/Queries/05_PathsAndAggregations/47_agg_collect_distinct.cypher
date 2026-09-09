MATCH (t:Type)-[:USES_TYPE]->(dep:Type)
RETURN t.name AS typeName, collect(DISTINCT dep.name) AS dependencies

