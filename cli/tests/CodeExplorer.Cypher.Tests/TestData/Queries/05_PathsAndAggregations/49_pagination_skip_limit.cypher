MATCH (f:File)
RETURN f.name AS fileName
ORDER BY f.name ASC
SKIP 1
LIMIT 10

