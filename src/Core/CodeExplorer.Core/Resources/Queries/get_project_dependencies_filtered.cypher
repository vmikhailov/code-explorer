MATCH (p:Project {name: $projectFilter})
WHERE p.id STARTS WITH $wsIdPrefix
OPTIONAL MATCH (p)-[:DEPENDS_ON]->(out)
OPTIONAL MATCH (in)-[:DEPENDS_ON]->(p)
RETURN p.name AS project, collect(DISTINCT out.name) AS outgoingDependencies, collect(DISTINCT in.name) AS incomingDependencies
