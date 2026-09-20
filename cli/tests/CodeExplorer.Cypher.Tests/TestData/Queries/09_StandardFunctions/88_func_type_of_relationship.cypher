MATCH (a:Function)-[r]->(b:Function)
RETURN type(r) AS relType
