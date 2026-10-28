MATCH path = (src:Function {symbol: $startFunction})-[:CALLS*1..{depth}]->(tgt:Function {symbol: $endFunction})
RETURN nodes(path) AS chain
