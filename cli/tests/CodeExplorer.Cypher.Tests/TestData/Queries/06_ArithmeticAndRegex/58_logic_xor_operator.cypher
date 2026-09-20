MATCH (n)
WHERE (n:Function) XOR (n:Type)
RETURN n.name AS name
