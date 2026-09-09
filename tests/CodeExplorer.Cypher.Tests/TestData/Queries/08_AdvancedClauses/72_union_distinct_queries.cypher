MATCH (f:Function)
RETURN f.name AS name
UNION
MATCH (t:Type)
RETURN t.name AS name
