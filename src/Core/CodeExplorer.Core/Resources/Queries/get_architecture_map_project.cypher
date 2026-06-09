MATCH (p:Project {name: $projectName}) {prefixFilter}
OPTIONAL MATCH (p)-[:CONTAINS]->(:DataBases)-[:USES_DB]->(db:DB)
WITH p, collect(DISTINCT db.name) AS projectDbs
OPTIONAL MATCH (p)-[:CONTAINS*1..]->(pf:ProjectFolder)
WITH p, projectDbs, collect(DISTINCT pf.name) AS folders
OPTIONAL MATCH (p)-[:CONTAINS*1..]->(es:ExternalService)
WITH p, projectDbs, folders, collect(DISTINCT es.name) AS projectEgress
OPTIONAL MATCH (p)-[:CONTAINS*1..]->(ep:EntryPoint)
WITH p, projectDbs, folders, projectEgress, collect(DISTINCT ep.name) AS projectIngress
OPTIONAL MATCH (p)-[:DEPENDS_ON]->(dep:Project)
RETURN p.name AS project, p.project_type AS language, folders,
       collect(DISTINCT dep.name) AS dependencies, projectDbs AS databases,
       projectIngress AS ingress, projectEgress AS egress
