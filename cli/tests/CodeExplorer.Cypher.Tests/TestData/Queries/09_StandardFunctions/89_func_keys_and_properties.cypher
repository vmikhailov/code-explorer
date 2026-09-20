MATCH (n:Project)
RETURN keys(n) AS propKeys, properties(n) AS allProps
