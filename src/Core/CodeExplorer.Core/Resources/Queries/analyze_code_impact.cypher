MATCH (target) WHERE (target:Type OR target:Function) AND (target.symbol = $symbolName OR target.name = $symbolName)
AND target.id STARTS WITH $wsIdPrefix
MATCH (target)<-[:USES_TYPE|CALLS]-(dependent)
OPTIONAL MATCH (dependent)-[:DECLARED_IN]->(f:File)
OPTIONAL MATCH (w:Workspace)-[:CONTAINS*1..]->(f)
RETURN CASE WHEN dependent:Type THEN (CASE WHEN dependent.kind = 'class' THEN 'Class' WHEN dependent.kind = 'interface' THEN 'Interface' ELSE dependent.kind END) ELSE labels(dependent)[0] END AS dependentType, dependent.name AS dependentName, dependent.symbol AS dependentSymbol,
CASE WHEN f IS NOT NULL AND w IS NOT NULL
     THEN w.path + '/' + f.path
     ELSE null END AS filePath
