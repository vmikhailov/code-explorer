MATCH (p:Project)-[:CONTAINS]->(f:File)
WITH *, count(f) AS fileCount
RETURN p.name AS projectName, fileCount
