MATCH p = allShortestPaths((a:Function)-[:CALLS*]->(b:Function))
RETURN p
