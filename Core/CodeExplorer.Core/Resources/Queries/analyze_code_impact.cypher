MATCH (target) WHERE (target:Class OR target:Interface OR target:Function) AND (target.symbol = $symbolName OR target.name = $symbolName)
AND target.id STARTS WITH $wsIdPrefix
MATCH (target)<-[:USES_TYPE|CALLS]-(dependent)
OPTIONAL MATCH (f:File)-[:DEFINES|DECLARES*1..]->(dependent)
OPTIONAL MATCH (w:Workspace)-[:CONTAINS*1..]->(f)
RETURN labels(dependent)[0] AS dependentType, dependent.name AS dependentName, dependent.symbol AS dependentSymbol,
CASE WHEN f IS NOT NULL AND w IS NOT NULL
     THEN w.path + '/' + f.path
     ELSE null END AS filePath
