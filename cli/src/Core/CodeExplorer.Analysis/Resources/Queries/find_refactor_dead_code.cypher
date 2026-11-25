MATCH (p:Project)<-[:BELONGS_TO]-(ps:ProjectSyntax)-[:CONTAINS]->(item)
WHERE ($projectName IS NULL OR $projectName = '' OR p.name = $projectName)
  AND (item:Function OR item:Type)
  AND (item.is_test IS NULL OR item.is_test = false)
  AND item.name <> '<module_init>'
  AND NOT item.name STARTS WITH 'Jest:'
OPTIONAL MATCH (item)-[:DECLARED_IN]->(f:File)
OPTIONAL MATCH (caller)-[:CALLS|USES_TYPE|INSTANTIATES|IMPLEMENTS|INHERITS_FROM|EXPOSED_BY|IMPLEMENTED_BY|OF_TYPE]->(item)
WITH f, item, caller
WHERE caller IS NULL
  AND NOT (item:Function AND item.name IN ['main', 'init'] AND coalesce(f.path, '') ENDS WITH '.go')
  AND (f IS NULL OR coalesce(f.artifact_role, 'source') <> 'vendor')
  AND NOT coalesce(f.path, '') CONTAINS '.config.'
  AND NOT coalesce(f.path, '') CONTAINS 'jest.setup'
  AND NOT coalesce(f.path, '') CONTAINS 'jest.env'
  AND NOT coalesce(f.path, '') ENDS WITH '.d.ts'
  AND NOT (
    item.name IN ['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS']
    AND (
      coalesce(f.path, '') ENDS WITH '/route.ts'
      OR coalesce(f.path, '') ENDS WITH '/route.tsx'
      OR coalesce(f.path, '') ENDS WITH '/route.js'
      OR coalesce(f.path, '') ENDS WITH '/route.jsx'
      OR coalesce(f.path, '') IN ['route.ts', 'route.tsx', 'route.js', 'route.jsx']
    )
  )
  AND NOT item.name IN [
    'generateMetadata', 'generateStaticParams', 'generateViewport',
    'generateImageMetadata', 'generateSitemaps',
    'getServerSideProps', 'getStaticProps', 'getStaticPaths',
    'middleware', 'proxy', 'register', 'onRequestError'
  ]
  AND NOT (
    item:Function
    AND item.is_exported = true
    AND (
      coalesce(f.path, '') ENDS WITH '/page.tsx' OR coalesce(f.path, '') ENDS WITH '/page.ts' OR coalesce(f.path, '') ENDS WITH '/page.jsx' OR coalesce(f.path, '') ENDS WITH '/page.js'
      OR coalesce(f.path, '') ENDS WITH '/layout.tsx' OR coalesce(f.path, '') ENDS WITH '/layout.ts' OR coalesce(f.path, '') ENDS WITH '/layout.jsx' OR coalesce(f.path, '') ENDS WITH '/layout.js'
      OR coalesce(f.path, '') ENDS WITH '/template.tsx' OR coalesce(f.path, '') ENDS WITH '/template.ts' OR coalesce(f.path, '') ENDS WITH '/template.jsx' OR coalesce(f.path, '') ENDS WITH '/template.js'
      OR coalesce(f.path, '') ENDS WITH '/loading.tsx' OR coalesce(f.path, '') ENDS WITH '/loading.ts' OR coalesce(f.path, '') ENDS WITH '/loading.jsx' OR coalesce(f.path, '') ENDS WITH '/loading.js'
      OR coalesce(f.path, '') ENDS WITH '/error.tsx' OR coalesce(f.path, '') ENDS WITH '/error.ts' OR coalesce(f.path, '') ENDS WITH '/error.jsx' OR coalesce(f.path, '') ENDS WITH '/error.js'
      OR coalesce(f.path, '') ENDS WITH '/global-error.tsx' OR coalesce(f.path, '') ENDS WITH '/global-error.ts' OR coalesce(f.path, '') ENDS WITH '/global-error.jsx' OR coalesce(f.path, '') ENDS WITH '/global-error.js'
      OR coalesce(f.path, '') ENDS WITH '/not-found.tsx' OR coalesce(f.path, '') ENDS WITH '/not-found.ts' OR coalesce(f.path, '') ENDS WITH '/not-found.jsx' OR coalesce(f.path, '') ENDS WITH '/not-found.js'
      OR coalesce(f.path, '') ENDS WITH '/default.tsx' OR coalesce(f.path, '') ENDS WITH '/default.ts' OR coalesce(f.path, '') ENDS WITH '/default.jsx' OR coalesce(f.path, '') ENDS WITH '/default.js'
      OR coalesce(f.path, '') ENDS WITH '/robots.ts' OR coalesce(f.path, '') ENDS WITH '/robots.js'
      OR coalesce(f.path, '') ENDS WITH '/sitemap.ts' OR coalesce(f.path, '') ENDS WITH '/sitemap.js'
      OR coalesce(f.path, '') ENDS WITH '/manifest.ts' OR coalesce(f.path, '') ENDS WITH '/manifest.js'
    )
  )
RETURN item.name AS name,
       CASE WHEN item:Type THEN (CASE WHEN item.kind = 'class' THEN 'Class' WHEN item.kind = 'interface' THEN 'Interface' ELSE item.kind END) ELSE labels(item)[0] END AS type,
       coalesce(f.path, '') AS filePath,
       'dead_code' AS anomalyType
LIMIT 20
