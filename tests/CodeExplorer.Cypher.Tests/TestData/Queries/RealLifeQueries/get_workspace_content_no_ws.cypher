MATCH (n)
WHERE $type IS NULL OR $type = '' OR any(lbl IN labels(n) WHERE lbl = $type)
RETURN n LIMIT 1000
