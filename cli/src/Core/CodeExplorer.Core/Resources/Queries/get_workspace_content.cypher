MATCH (n)
WHERE $type IS NULL OR $type = '' OR any(lbl IN labels(n) WHERE lbl = $type)
RETURN coalesce(n.name, n.id) AS name,
       labels(n)[0] AS type,
       coalesce(n.path, '') AS path
LIMIT 25
