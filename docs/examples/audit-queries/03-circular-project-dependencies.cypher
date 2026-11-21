// Bidirectional Project Dependencies & Chatty Service Pairs (Merge / Repackage candidates)
MATCH (a:Project)-[:DEPENDS_ON]->(b:Project)-[:DEPENDS_ON]->(a)
WHERE a.name < b.name
RETURN a.name AS projectA,
       b.name AS projectB,
       coalesce(a.project_type, '') AS langA,
       coalesce(b.project_type, '') AS langB
ORDER BY projectA, projectB
LIMIT 100
