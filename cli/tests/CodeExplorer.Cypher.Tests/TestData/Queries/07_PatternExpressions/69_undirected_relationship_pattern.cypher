MATCH (a:File)--(b:File)
RETURN a.name AS fileA, b.name AS fileB
