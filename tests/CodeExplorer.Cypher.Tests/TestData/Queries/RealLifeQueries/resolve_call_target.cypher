MATCH (i:Type {name: $interfaceName, kind: 'interface'})<-[:IMPLEMENTS]-(impl:Type {kind: 'class'})-[:HAS_METHOD]->(f:Function {name: $methodName})
WHERE i.id STARTS WITH $wsIdPrefix
RETURN impl.name AS className, f.name AS methodName, f.symbol AS methodSymbol, f.file_path AS filePath, f.start_line AS startLine
