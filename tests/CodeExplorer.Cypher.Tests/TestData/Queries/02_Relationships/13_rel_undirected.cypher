MATCH (a:Function)-[:CALLS]-(b:Function)
RETURN a.name AS callerOrCallee, b.name AS counterpart

