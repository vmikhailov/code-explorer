MATCH (p:Project {name: $projectName})
OPTIONAL MATCH (p)-[:LOCATED_IN]->(target:Folder)-[:CONTAINS*0..3]->(pf:Folder)
OPTIONAL MATCH (db:Database) WHERE db.id STARTS WITH p.id
WITH p, collect(DISTINCT pf.name) AS folders, collect(DISTINCT db.name) AS projectDbs
OPTIONAL MATCH (p)-[:DEPENDS_ON]->(dep:Project)
RETURN p.name AS project, p.project_type AS language, folders,
       collect(DISTINCT dep.name) AS dependencies, projectDbs AS databases,
       [(p)<-[:BELONGS_TO]-(psem:ProjectSemantic)-[:CONTAINS]->(ep) WHERE ep:Endpoint OR ep:EntryPoint | ep.name] AS ingress,
       [(p)<-[:BELONGS_TO]-(psem:ProjectSemantic)-[:CONTAINS]->(es:ExternalService) | es.name] AS egress
