MATCH (w:Workspace)
OPTIONAL MATCH (w)-[:CONTAINS]->(ps:ProjectsStructure)<-[:LOCATED_IN]-(p:Project)
OPTIONAL MATCH (db:Database)
OPTIONAL MATCH (es:ExternalService)
WITH w,
     collect(DISTINCT {
         name: p.name,
         language: p.project_type,
         path: p.path
     }) AS rawProjects,
     collect(DISTINCT db.name) AS databases,
     collect(DISTINCT es.name) AS externalServices
WITH w, [x IN rawProjects WHERE x.name IS NOT NULL] AS projects, databases, externalServices
RETURN w.name AS workspace, w.path AS path,
       size(projects) AS totalProjects,
       projects,
       databases,
       externalServices
