MATCH path = (src:Function)-[:CALLS*..5]->(tgt:Function)
RETURN nodes(path) AS chain

