MATCH (t:Type)
WHERE NOT (t.kind = 'class')
RETURN t.name AS typeName

