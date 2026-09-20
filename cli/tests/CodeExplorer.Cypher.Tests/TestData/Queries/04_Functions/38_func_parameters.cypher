MATCH (n:Type)
WHERE n.symbol = $symbolParam
RETURN n.name AS typeName, n.kind AS typeKind

