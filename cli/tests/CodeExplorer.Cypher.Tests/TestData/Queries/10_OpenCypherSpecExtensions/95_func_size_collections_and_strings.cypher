MATCH (n:Function)
RETURN size(['a', 'b', 'c']) AS listSize, size(n.name) AS nameSize
