MATCH (n:Function)
RETURN n.name AS name, ['a', 'b', 'c', 'd'][1..3] AS sliced
