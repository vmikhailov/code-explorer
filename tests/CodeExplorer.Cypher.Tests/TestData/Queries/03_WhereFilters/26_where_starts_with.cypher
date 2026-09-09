MATCH (n:Type)
WHERE n.symbol STARTS WITH 'MyProject.'
RETURN n.name AS typeName

