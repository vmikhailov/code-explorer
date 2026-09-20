MATCH (p:Project)
CALL {
    WITH p
    MATCH (p)-[:CONTAINS]->(f:File)
    RETURN count(f) AS fileCount
}
RETURN p.name AS projectName, fileCount
