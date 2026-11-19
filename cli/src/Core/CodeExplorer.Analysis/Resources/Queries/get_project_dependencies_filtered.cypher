MATCH (p:Project)
WHERE $projectFilter IS NULL OR $projectFilter = '' OR p.name = $projectFilter
OPTIONAL MATCH (p)-[:DEPENDS_ON]->(out)
OPTIONAL MATCH (in)-[:DEPENDS_ON]->(p)
RETURN p.name AS project, collect(DISTINCT out.name) AS outgoingDependencies, collect(DISTINCT in.name) AS incomingDependencies
