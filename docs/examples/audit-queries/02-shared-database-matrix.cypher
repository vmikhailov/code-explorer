// Shared Database & Table Ownership Matrix (flags tables/DBs touched by multiple services/projects)
MATCH (actor)-[r:USES_DB|READS_FROM|WRITES_TO]->(asset)
WHERE (actor:Service OR actor:App OR actor:Worker OR actor:Project)
  AND (asset:Database OR asset:Table OR asset:Topic)
WITH asset, labels(asset)[0] AS assetKind, collect(DISTINCT actor.name) AS consumers, count(DISTINCT actor) AS consumerCount
WHERE consumerCount > 1
RETURN assetKind, asset.name AS assetName, consumerCount, consumers
ORDER BY consumerCount DESC, assetName
LIMIT 100
