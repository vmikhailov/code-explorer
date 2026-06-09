MATCH path = (src:Function {symbol: $startFunction})-[:CALLS*1..{depth}]->(tgt:Function {symbol: $endFunction})
WHERE src.id STARTS WITH $wsIdPrefix AND tgt.id STARTS WITH $wsIdPrefix
RETURN nodes(path) AS chain
