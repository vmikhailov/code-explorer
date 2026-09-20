MATCH (n:Function)
WHERE single(x IN ['a'] WHERE x = 'a') AND exists(n.name)
RETURN n.name AS name
