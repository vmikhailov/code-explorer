MATCH (target) WHERE (target:Type OR target:Function OR target:Project OR target:ExternalService OR target:Table OR target:Topic) 
  AND (target.symbol = $symbolName 
       OR target.name = $symbolName 
       OR target.id = $symbolName
       OR target.graphql_field = $symbolName 
       OR target.graphql_operation = $symbolName
       OR target.symbol ENDS WITH $symbolName 
       OR target.name CONTAINS $symbolName)
MATCH (target)<-[r:USES_TYPE|CALLS|SERVICE_CALL|CALLS_ENDPOINT|IMPLEMENTS|INHERITS_FROM|PUBLISHES_TO|SUBSCRIBES_TO|TRIGGERS|QUERIED_BY|PERSISTED_IN]-(dependent)
OPTIONAL MATCH (dependent)-[:DECLARED_IN]->(f:File)
RETURN 
  CASE 
    WHEN dependent:Type THEN (CASE WHEN dependent.kind = 'class' THEN 'Class' WHEN dependent.kind = 'interface' THEN 'Interface' ELSE dependent.kind END) 
    WHEN dependent:Function THEN 'Function'
    WHEN dependent:Project THEN 'Service'
    WHEN dependent:Table THEN 'Table'
    WHEN dependent:Topic THEN 'Topic'
    ELSE labels(dependent)[0] 
  END AS dependentType, 
  dependent.name AS dependentName, 
  dependent.symbol AS dependentSymbol,
  type(r) AS relationship,
  CASE 
    WHEN COALESCE(dependent.path, dependent.file_path, f.path) IS NOT NULL AND dependent.start_line IS NOT NULL 
    THEN COALESCE(dependent.path, dependent.file_path, f.path) + ':' + toString(dependent.start_line)
    WHEN COALESCE(dependent.path, dependent.file_path, f.path) IS NOT NULL 
    THEN COALESCE(dependent.path, dependent.file_path, f.path)
    ELSE '' 
  END AS filePath
ORDER BY dependentType, dependentName
