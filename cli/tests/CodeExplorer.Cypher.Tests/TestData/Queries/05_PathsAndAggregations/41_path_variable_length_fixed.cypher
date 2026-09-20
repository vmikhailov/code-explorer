MATCH path = (src:Function)-[:CALLS*1..3]->(tgt:Function)
RETURN nodes(path) AS chain

