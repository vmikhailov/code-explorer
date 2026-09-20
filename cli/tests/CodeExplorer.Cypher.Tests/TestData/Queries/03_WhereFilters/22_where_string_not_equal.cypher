MATCH (t:Type)
WHERE t.kind <> 'interface'
RETURN t.name AS typeName

