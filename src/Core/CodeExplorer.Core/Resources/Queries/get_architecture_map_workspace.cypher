MATCH (w:Workspace) WHERE w.id = $workspaceId OR toString(w.id) = toString($workspaceId)
OPTIONAL MATCH (w)-[:CONTAINS*1..]->(wf:WorkspaceFolder)
WITH w, collect(DISTINCT wf.name) AS workspaceFolders
OPTIONAL MATCH (w)-[:CONTAINS]->(:ProjectsStructure)<-[:LOCATED_IN]-(p:Project)
OPTIONAL MATCH (p)-[:LOCATED_IN]->(target) WHERE NOT target:ProjectsStructure
WITH w, workspaceFolders, p, target
OPTIONAL MATCH (target)-[:CONTAINS*0..]->(file:File)
WITH w, workspaceFolders, p, target, collect(DISTINCT file) AS files
WITH w, workspaceFolders, p, target, files, [f IN files | f.path] AS filePaths

OPTIONAL MATCH (db:Database)<-[:USES_DB]-(file:File) WHERE file IN files
WITH w, workspaceFolders, p, target, files, filePaths, collect(DISTINCT db.name) AS projectDbs

OPTIONAL MATCH (es:ExternalService) WHERE es.file_path IN filePaths
WITH w, workspaceFolders, p, target, files, filePaths, projectDbs, collect(DISTINCT es.name) AS projectEgress

OPTIONAL MATCH (ep) WHERE (ep:Endpoint OR ep:EntryPoint) AND ep.path IN filePaths
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
