MATCH (p:Project)-[:DEPENDS_ON]->(dep)
WHERE p.id STARTS WITH $wsIdPrefix
RETURN p.name AS project, dep.name AS dependency, labels(dep)[0] AS dependencyType
