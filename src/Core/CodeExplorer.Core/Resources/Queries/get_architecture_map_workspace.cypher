MATCH (w:Workspace) WHERE w.id = $workspaceId OR toString(w.id) = toString($workspaceId)
OPTIONAL MATCH (w)-[:CONTAINS*1..]->(wf:WorkspaceFolder)
WITH w, collect(DISTINCT wf.name) AS workspaceFolders
OPTIONAL MATCH (w)-[:CONTAINS]->(:ProjectsStructure)<-[:LOCATED_IN]-(p:Project)
OPTIONAL MATCH (p)-[:LOCATED_IN]->(target) WHERE NOT target:ProjectsStructure
WITH w, workspaceFolders, p, target
OPTIONAL MATCH (target)-[:CONTAINS*0..]->(file:File)-[:USES_DB]->(db:Database)
WITH w, workspaceFolders, p, target, collect(DISTINCT db.name) AS projectDbs
OPTIONAL MATCH (target)-[:CONTAINS*0..]->(file:File)
OPTIONAL MATCH (es:ExternalService) WHERE es.file_path = file.path
WITH w, workspaceFolders, p, target, projectDbs, collect(DISTINCT es.name) AS projectEgress
OPTIONAL MATCH (target)-[:CONTAINS*0..]->(file:File)
OPTIONAL MATCH (ep) WHERE (ep:Endpoint OR ep:EntryPoint) AND ep.path = file.path
WITH w, workspaceFolders, p, projectDbs, projectEgress, collect(DISTINCT ep.name) AS projectIngress
OPTIONAL MATCH (p)-[:DEPENDS_ON]->(dep:Project)
WITH w, workspaceFolders, p, projectDbs, projectEgress, projectIngress, collect(DISTINCT dep.name) AS projectDeps
WITH w, workspaceFolders,
     collect(DISTINCT {
         name: p.name,
         language: p.project_type,
         dependencies: projectDeps,
         databases: projectDbs,
         ingress: projectIngress,
         egress: projectEgress
     }) AS projectsRaw
WITH w, workspaceFolders, [x IN projectsRaw WHERE x.name IS NOT NULL] AS projects
RETURN w.name AS workspace, w.path AS path, workspaceFolders, projects
