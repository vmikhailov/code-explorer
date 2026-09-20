MATCH (n:Function)
WHERE n.name =~ '^Get.*'
RETURN n.name AS name
