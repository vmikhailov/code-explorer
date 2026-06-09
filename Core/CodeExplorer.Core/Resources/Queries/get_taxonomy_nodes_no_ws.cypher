MATCH (n)-[r]->(m)
WITH DISTINCT labels(n)[0] AS fromLabel, type(r) AS relType, labels(m)[0] AS toLabel RETURN fromLabel, relType, toLabel
