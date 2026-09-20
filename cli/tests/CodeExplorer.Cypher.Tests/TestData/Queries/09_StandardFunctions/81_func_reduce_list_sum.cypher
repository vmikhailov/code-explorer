MATCH (n:Function)
RETURN reduce(acc = 0, x IN [1, 2, 3, 4] | acc + x) AS totalSum
