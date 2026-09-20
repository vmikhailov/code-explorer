MATCH path = (a:Function)-[r:CALLS]->(b:Function)
RETURN relationships(path) AS rels
