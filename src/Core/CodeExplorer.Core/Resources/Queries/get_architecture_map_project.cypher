MATCH (p:Project {name: $projectName}) {prefixFilter}
MATCH (p)-[:LOCATED_IN]->(target) WHERE NOT target:ProjectsStructure
OPTIONAL MATCH (target)-[:CONTAINS*1..]->(pf:Folder)
WITH p, target, collect(DISTINCT pf.name) AS folders
OPTIONAL MATCH (target)-[:CONTAINS*0..]->(file:File)-[:USES_DB]->(db:Database)
WITH p, target, folders, collect(DISTINCT db.name) AS projectDbs
OPTIONAL MATCH (target)-[:CONTAINS*0..]->(file:File)
OPTIONAL MATCH (es:ExternalService) WHERE es.file_path = file.path
WITH p, target, folders, projectDbs, collect(DISTINCT es.name) AS projectEgress
OPTIONAL MATCH (target)-[:CONTAINS*0..]->(file:File)
OPTIONAL MATCH (ep) WHERE (ep:Endpoint OR ep:EntryPoint) AND ep.path = file.path
WITH p, folders, projectDbs, projectEgress, collect(DISTINCT ep.name) AS projectIngress
OPTIONAL MATCH (p)-[:DEPENDS_ON]->(dep:Project)
RETURN p.name AS project, p.project_type AS language, folders,
       collect(DISTINCT dep.name) AS dependencies, projectDbs AS databases,
       projectIngress AS ingress, projectEgress AS egress
