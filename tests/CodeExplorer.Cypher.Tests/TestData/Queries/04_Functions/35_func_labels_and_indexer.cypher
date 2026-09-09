MATCH (n)
WHERE n:Type
RETURN n.name AS typeName, labels(n)[0] AS primaryLabel

