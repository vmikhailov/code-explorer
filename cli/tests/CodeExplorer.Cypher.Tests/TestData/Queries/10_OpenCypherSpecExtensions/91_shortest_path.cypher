MATCH p = shortestPath((a:Function)-[:CALLS*]->(b:Function))
RETURN p
