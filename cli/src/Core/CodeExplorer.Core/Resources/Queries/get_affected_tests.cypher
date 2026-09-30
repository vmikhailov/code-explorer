MATCH (target:Function)
WHERE ($filePath IS NOT NULL AND (target.filePath = $filePath OR target.filePath ENDS WITH $filePath OR target.path = $filePath OR target.path ENDS WITH $filePath))
   OR ($symbolName IS NOT NULL AND (target.symbol = $symbolName OR target.name = $symbolName))
MATCH (test:Function)-[:CALLS*0..15]->(target)
WHERE test.is_test = 'true'
RETURN DISTINCT
    test.name AS testName,
    test.symbol AS testSymbol,
    test.filePath AS testFilePath,
    test.test_framework AS testFramework,
    target.name AS affectedMethodName,
    target.filePath AS affectedFilePath
ORDER BY test.filePath, test.name
