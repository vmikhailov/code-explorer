MATCH (t:Table {name: $tableName})
OPTIONAL MATCH (t)-[:QUERIED_BY|DEPENDS_ON]-(q:Query)
OPTIONAL MATCH (parent)-[:DEFINES|DECLARES]->(q)
OPTIONAL MATCH (caller)-[:CALLS]->(parent)
RETURN t.name AS tableName, q.name AS queryName, q.query_text AS queryText, q.path AS filePath,
       collect(DISTINCT parent.name) AS parentName, labels(parent)[0] AS parentType,
       collect(DISTINCT caller.name) AS callingSymbols
