// Orphan Projects & Sunset/Repackage Candidates (0 inbound projects and 0 exposed endpoints)
MATCH (p:Project)
WHERE coalesce(p.is_test, false) = false
  AND NOT EXISTS((:Project)-[:DEPENDS_ON]->(p))
  AND NOT EXISTS((:Endpoint)-[:DECLARED_IN]->(p))
OPTIONAL MATCH (p)-[:CONTAINS]->(f:File)
RETURN p.name AS project,
       coalesce(p.project_type, p.language, '') AS language,
       coalesce(p.entity_kind, '') AS entityKind,
       p.path AS path,
       count(DISTINCT f) AS files
ORDER BY files DESC
LIMIT 100
