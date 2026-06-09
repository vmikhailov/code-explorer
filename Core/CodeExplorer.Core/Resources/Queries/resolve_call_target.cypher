MATCH (i:Interface {name: $interfaceName})<-[:IMPLEMENTS]-(impl:Class)-[:DECLARES]->(f:Function {name: $methodName})
WHERE i.id STARTS WITH $wsIdPrefix
RETURN impl.name AS className, f.name AS methodName, f.symbol AS methodSymbol, f.file_path AS filePath, f.start_line AS startLine
