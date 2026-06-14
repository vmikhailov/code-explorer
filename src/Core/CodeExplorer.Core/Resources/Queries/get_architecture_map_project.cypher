MATCH (p:Project {name: $projectName}) {prefixFilter}
MATCH (p)-[:LOCATED_IN]->(target) WHERE NOT target:ProjectsStructure
OPTIONAL MATCH (target)-[:CONTAINS*1..]->(pf:Folder)
WITH p, target, collect(DISTINCT pf.name) AS folders
OPTIONAL MATCH (target)-[:CONTAINS*0..]->(file:File)
WITH p, target, folders, collect(DISTINCT file) AS files
WITH p, target, folders, files, [f IN files | f.path] AS filePaths

OPTIONAL MATCH (db:Database)<-[:USES_DB]-(file:File) WHERE file IN files
WITH p, target, folders, files, filePaths, collect(DISTINCT db.name) AS projectDbs

OPTIONAL MATCH (es:ExternalService) WHERE es.file_path IN filePaths
WITH p, target, folders, files, filePaths, projectDbs, collect(DISTINCT es.name) AS projectEgress

OPTIONAL MATCH (ep) WHERE (ep:Endpoint OR ep:EntryPoint) AND ep.path IN filePaths
WITH p, folders, projectDbs, projectEgress, collect(DISTINCT ep.name) AS projectIngress

OPTIONAL MATCH (p)-[:DEPENDS_ON]->(dep:Project)
RETURN p.name AS project, p.project_type AS language, folders,
       collect(DISTINCT dep.name) AS dependencies, projectDbs AS databases,
       projectIngress AS ingress, projectEgress AS egress
