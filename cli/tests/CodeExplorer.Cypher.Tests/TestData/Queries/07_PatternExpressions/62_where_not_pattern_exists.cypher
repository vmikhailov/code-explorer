MATCH (fn:Function)
WHERE NOT (fn)-[:CALLS]->()
RETURN fn.name AS leafFunction
