MATCH (src)-[:DEFINES|DECLARES]->(tgt)
RETURN src.id AS srcId, tgt.id AS tgtId

