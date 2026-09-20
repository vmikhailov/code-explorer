MATCH (n:Function)
WHERE n.name =~ '(?i).*service.*'
RETURN n.name AS name
