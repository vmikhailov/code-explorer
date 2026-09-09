MATCH (w:Workspace)-[:CONTAINS]->(f:File)-[:DEFINES]->(t:Type)
OPTIONAL MATCH (t)-[:HAS_METHOD]->(m:Function)
OPTIONAL MATCH path = (m)-[:CALLS*1..3]->(called:Function)
WHERE w.name STARTS WITH 'My' AND (t.kind = 'class' OR t.kind IS NULL)
RETURN w.name AS wsName, f.name AS fileName, t.name AS typeName, m.name AS methodName, count(called) AS callCount, collect(DISTINCT called.name) AS downstreamCalls
ORDER BY typeName ASC, methodName ASC
LIMIT 25

