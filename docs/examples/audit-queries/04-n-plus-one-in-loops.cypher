// Potential N+1 Hotspots: calls executed inside loops targeting DB/HTTP/downstream functions
MATCH (caller:Function)-[r:CALLS]->(callee)
WHERE r.in_loop = true
  AND (callee:Function OR callee:Query OR callee:Table OR callee:ExternalService)
RETURN caller.name AS caller,
       coalesce(caller.file_path, caller.path, '') AS filePath,
       coalesce(r.line, caller.start_line, 0) AS line,
       coalesce(r.loop_depth, 1) AS loopDepth,
       labels(callee)[0] AS targetKind,
       callee.name AS target
ORDER BY loopDepth DESC, filePath, line
LIMIT 100
