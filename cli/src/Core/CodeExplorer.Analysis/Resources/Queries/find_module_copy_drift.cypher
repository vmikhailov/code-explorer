MATCH (f1:File), (f2:File)
WHERE f1.name = f2.name
  AND f1.path < f2.path
  AND coalesce(f1.is_test, false) = false
  AND coalesce(f2.is_test, false) = false
  AND coalesce(f1.artifact_role, 'source') <> 'vendor'
  AND coalesce(f2.artifact_role, 'source') <> 'vendor'
  AND NOT f1.name IN ['index.php', 'index.js', 'index.ts', '__init__.py', 'bootstrap.php', 'Bootstrap.php', 'config.php', 'settings.py', 'Dockerfile', 'Makefile', 'README.md', 'composer.json', 'package.json', 'requirements.txt']
WITH f1, f2,
     coalesce(f1.line_count, 0) AS lines1,
     coalesce(f2.line_count, 0) AS lines2,
     CASE WHEN f1.content_hash IS NOT NULL AND f1.content_hash = f2.content_hash THEN 'identical' ELSE 'drifted' END AS status
RETURN f1.name AS fileName,
       status,
       f1.path AS pathA,
       f2.path AS pathB,
       lines1 AS linesA,
       lines2 AS linesB,
       abs(lines1 - lines2) AS lineDelta
ORDER BY status ASC, lineDelta DESC, fileName ASC
LIMIT 100
