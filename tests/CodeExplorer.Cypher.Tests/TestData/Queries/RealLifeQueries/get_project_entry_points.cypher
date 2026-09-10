MATCH (p:Project {name: $projectName})-[:CONTAINS*1..]->(f:File) WHERE p.id STARTS WITH $wsIdPrefix
MATCH (f)-[:DEFINES|DECLARES*1..]->(func:Function)
WHERE f.path CONTAINS 'Controller' OR f.path CONTAINS 'Endpoint' OR f.path CONTAINS 'Handler' OR f.path CONTAINS 'Resolver' OR func.name STARTS WITH 'On' OR func.name STARTS WITH 'Handle'
OPTIONAL MATCH (class:Type {kind: 'class'})-[:HAS_METHOD]->(func)
RETURN func.name AS entryPoint, func.symbol AS symbol, class.name AS className, f.path AS filePath, func.start_line AS startLine
