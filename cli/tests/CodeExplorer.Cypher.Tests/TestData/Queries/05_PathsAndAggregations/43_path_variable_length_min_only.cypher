MATCH path = (src:Function)-[:CALLS*2..]->(tgt:Function)
RETURN nodes(path) AS chain

