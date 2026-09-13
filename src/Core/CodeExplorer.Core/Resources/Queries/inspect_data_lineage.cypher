MATCH (t:Table {name: $tableName})
OPTIONAL MATCH (q:Query)-[:DEPENDS_ON]->(t)
OPTIONAL MATCH (parent)-[:DEFINES|DECLARES]->(q)
OPTIONAL MATCH (caller)-[:CALLS|DEPENDS_ON*0..]->(parent)
RETURN t.name AS tableName, q.name AS queryName, q.query_text AS queryText, q.path AS filePath,
       collect(DISTINCT parent.name) AS parentName, labels(parent)[0] AS parentType,
       collect(DISTINCT caller.name) AS callingSymbols
