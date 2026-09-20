MATCH (n:Type)
RETURN n.name AS typeName, coalesce(n.kind, 'unknown') AS effectiveKind

