MATCH (p:Project)-[:CONTAINS*0..1]->(target)
RETURN p.name AS project, target.name AS targetName
