MATCH (w:Workspace)
OPTIONAL MATCH (w)-[:CONTAINS]->(:ProjectsStructure)<-[:LOCATED_IN]-(p:Project)
OPTIONAL MATCH (p)-[:LOCATED_IN]->(target) WHERE NOT target:ProjectsStructure
WITH w, p, target
OPTIONAL MATCH (target)-[:CONTAINS*0..]->(file:File)
WITH w, p, target, collect(DISTINCT file) AS files
WITH w, p, target, files, [f IN files | f.path] AS filePaths

OPTIONAL MATCH (db:Database)<-[:USES_DB]-(file:File) WHERE file IN files
WITH w, p, target, files, filePaths, collect(DISTINCT db.name) AS projectDbs

OPTIONAL MATCH (es:ExternalService) WHERE es.file_path IN filePaths
WITH w, p, target, files, filePaths, projectDbs, collect(DISTINCT es.name) AS projectEgress

OPTIONAL MATCH (ep1:Endpoint) WHERE ep1.path IN filePaths
WITH w, p, projectDbs, projectEgress, collect(DISTINCT ep1.name) AS eps1
OPTIONAL MATCH (ep2:EntryPoint) WHERE ep2.path IN filePaths
WITH w, p, projectDbs, projectEgress, eps1, collect(DISTINCT ep2.name) AS eps2
WITH w, p, projectDbs, projectEgress, (eps1 + eps2) AS projectIngress

OPTIONAL MATCH (p)-[:DEPENDS_ON]->(dep:Project)
WITH w, p, projectDbs, projectEgress, projectIngress, collect(DISTINCT dep.name) AS projectDeps
WITH w,
     collect(DISTINCT {
         name: p.name,
         language: p.project_type,
         dependencies: projectDeps,
         databases: projectDbs,
         ingress: projectIngress,
         egress: projectEgress
     }) AS projectsRaw
WITH w, [x IN projectsRaw WHERE x.name IS NOT NULL] AS projects
RETURN w.name AS workspace, w.path AS path, projects
