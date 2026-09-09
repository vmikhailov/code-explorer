MATCH (a:Function)-[:CALLS*3]->(b:Function)
RETURN a.name AS caller, b.name AS target
