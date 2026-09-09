MATCH (p:Project)-[:CONTAINS]->(f:File)
WITH p, f
MATCH (f)-[:DEFINES]->(fn:Function)
WITH p, count(fn) AS fnCount
WHERE fnCount > 5
RETURN p.name AS projectName, fnCount
