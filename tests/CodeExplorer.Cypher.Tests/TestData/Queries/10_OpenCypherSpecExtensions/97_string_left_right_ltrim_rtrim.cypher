MATCH (n:Function)
RETURN left(n.name, 3) AS l, right(n.name, 3) AS r, ltrim(' test') AS lt, rtrim('test ') AS rt
