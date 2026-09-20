MATCH (n)
WITH DISTINCT labels(n) AS labels, keys(n) AS keys UNWIND labels AS label UNWIND keys AS key RETURN DISTINCT label, key
