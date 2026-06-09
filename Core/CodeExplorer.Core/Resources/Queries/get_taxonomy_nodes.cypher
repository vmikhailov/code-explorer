MATCH (n)-[r]->(m) WHERE toString(n.id) STARTS WITH $wsIdPrefix AND toString(m.id) STARTS WITH $wsIdPrefix
WITH DISTINCT labels(n)[0] AS fromLabel, type(r) AS relType, labels(m)[0] AS toLabel RETURN fromLabel, relType, toLabel
