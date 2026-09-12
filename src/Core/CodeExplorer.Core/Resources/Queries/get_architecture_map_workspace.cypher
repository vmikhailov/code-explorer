MATCH (w:Workspace) WHERE w.id = $workspaceId OR toString(w.id) = toString($workspaceId)
MATCH (w)-[:CONTAINS]->(ps:ProjectsStructure)
OPTIONAL MATCH (ps)<-[:LOCATED_IN]-(p:Project)
OPTIONAL MATCH (db:Database) WHERE db.id STARTS WITH p.id
WITH w, p, collect(DISTINCT db.name) AS projectDbs
WITH w,
     collect(DISTINCT {
         name: p.name,
         language: p.project_type,
         dependencies: [(p)-[:DEPENDS_ON]->(dep:Project) | dep.name],
         databases: projectDbs,
         ingress: [(p)<-[:BELONGS_TO]-(psem:ProjectSemantic)-[:CONTAINS]->(ep) WHERE ep:Endpoint OR ep:EntryPoint | ep.name],
         egress: [(p)<-[:BELONGS_TO]-(psem:ProjectSemantic)-[:CONTAINS]->(es:ExternalService) | es.name]
     }) AS projectsRaw
WITH w, [x IN projectsRaw WHERE x.name IS NOT NULL] AS projects
RETURN w.name AS workspace, w.path AS path, projects

