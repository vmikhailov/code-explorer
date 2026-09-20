MATCH (f:Function)
RETURN f.name AS name, 'Function' AS kind
UNION ALL
MATCH (t:Type)
RETURN t.name AS name, 'Type' AS kind
