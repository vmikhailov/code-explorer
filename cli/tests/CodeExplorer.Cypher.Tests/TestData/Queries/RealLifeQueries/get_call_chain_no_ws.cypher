MATCH path = (src:Function {symbol: $startFunction})-[:CALLS*1..5]->(tgt:Function {symbol: $endFunction})
RETURN nodes(path) AS chain
