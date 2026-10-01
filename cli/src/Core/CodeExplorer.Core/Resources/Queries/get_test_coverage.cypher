MATCH (item)
WHERE (item:Function OR item:Type)
  AND (item.is_test IS NULL OR item.is_test <> 'true')
  AND ($projectName IS NULL OR item.project = $projectName)
OPTIONAL MATCH (test:Function)-[:CALLS*1..10]->(item)
WHERE test.is_test = 'true'
WITH item, count(DISTINCT test) AS coveringTestCount, collect(DISTINCT test.name) AS coveringTests
RETURN
    item.name AS name,
    item.symbol AS symbol,
    CASE WHEN item:Type THEN 'Class' ELSE 'Method' END AS kind,
    coalesce(item.file_path, item.path, '') AS filePath,
    item.project AS project,
    CASE WHEN coveringTestCount > 0 THEN 'Covered' ELSE 'Uncovered' END AS status,
    coveringTestCount AS coveringTestCount,
    coveringTests AS coveringTests
ORDER BY status, filePath, name
