MATCH (p:Project {name: $projectName})-[:CONTAINS*1..]->(f:File) {prefixClause}
MATCH (f)-[:DEFINES|DECLARES*1..]->(item) WHERE (item:Function OR item:Class)
OPTIONAL MATCH (caller:Entity)-[:CALLS|USES_TYPE]->(item) WITH f, item, caller
WHERE caller IS NULL
RETURN item.name AS name, labels(item)[0] AS type, f.path AS filePath, 'dead_code' AS anomalyType, item.symbol AS symbol LIMIT 50
