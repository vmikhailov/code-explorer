MATCH (n)
WHERE (n:Type OR n:Function) AND n.name = 'SaveToDb'
RETURN n.id AS nodeId

