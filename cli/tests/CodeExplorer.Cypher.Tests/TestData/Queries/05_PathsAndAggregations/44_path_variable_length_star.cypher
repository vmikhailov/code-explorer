MATCH path = (src:Function)-[:CALLS*]->(tgt:Function)
RETURN nodes(path) AS chain

