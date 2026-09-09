MATCH (fn:Function)
WHERE (fn)-[:CALLS]->(:Function)
RETURN fn.name AS caller
