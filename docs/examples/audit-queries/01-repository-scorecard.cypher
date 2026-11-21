// Repository Scorecard: quantitative metrics per project for Keep / Merge / Repackage / Sunset
MATCH (p:Project)
OPTIONAL MATCH (p)-[:CONTAINS]->(f:File)
OPTIONAL MATCH (inbound:Project)-[:DEPENDS_ON]->(p)
OPTIONAL MATCH (p)-[:DEPENDS_ON]->(outbound:Project)
OPTIONAL MATCH (ep:Endpoint)-[:DECLARED_IN]->(p)
RETURN p.name AS project,
       coalesce(p.project_type, p.language, '') AS language,
       coalesce(p.entity_kind, '') AS entityKind,
       count(DISTINCT f) AS files,
       sum(coalesce(f.line_count, 0)) AS loc,
       count(DISTINCT inbound) AS inboundProjects,
       count(DISTINCT outbound) AS outboundProjects,
       count(DISTINCT ep) AS endpoints
ORDER BY loc DESC
LIMIT 100
