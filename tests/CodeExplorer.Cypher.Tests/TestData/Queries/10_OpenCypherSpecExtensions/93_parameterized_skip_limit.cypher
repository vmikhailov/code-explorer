MATCH (n:Function)
RETURN n.name AS name
SKIP $skip
LIMIT $limit
