MATCH (p:Project {name: $projectName}) {prefixFilter}
MATCH (p)-[:LOCATED_IN]->(f:Folder)
OPTIONAL MATCH (f)-[:CONTAINS*1..]->(pf:Folder)
WITH p, f, collect(DISTINCT pf.name) AS folders
OPTIONAL MATCH (f)-[:CONTAINS*0..]->(file:File)-[:USES_DB]->(db:Database)
WITH p, f, folders, collect(DISTINCT db.name) AS projectDbs
OPTIONAL MATCH (f)-[:CONTAINS*0..]->(file:File)
OPTIONAL MATCH (es:ExternalService) WHERE es.file_path = file.path
WITH p, f, folders, projectDbs, collect(DISTINCT es.name) AS projectEgress
OPTIONAL MATCH (f)-[:CONTAINS*0..]->(file:File)
OPTIONAL MATCH (ep) WHERE (ep:Endpoint OR ep:EntryPoint) AND ep.path = file.path
WITH p, folders, projectDbs, projectEgress, collect(DISTINCT ep.name) AS projectIngress
OPTIONAL MATCH (p)-[:DEPENDS_ON]->(dep:Project)
RETURN p.name AS project, p.project_type AS language, folders,
       collect(DISTINCT dep.name) AS dependencies, projectDbs AS databases,
       projectIngress AS ingress, projectEgress AS egress
