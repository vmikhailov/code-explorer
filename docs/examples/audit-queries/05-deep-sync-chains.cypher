// Deep Synchronous Call Chains from Endpoints to Data / External Services
MATCH (ep:Endpoint)-[:TRIGGERS|EXPOSED_BY]->(entry:Function)-[:CALLS*1..4]->(leaf:Function)-[r:READS_FROM|WRITES_TO|SERVICE_CALL|PUBLISHES_TO]->(sink)
RETURN ep.name AS endpoint,
       entry.name AS entryFunction,
       leaf.name AS leafFunction,
       type(r) AS sinkRel,
       labels(sink)[0] AS sinkKind,
       sink.name AS sinkName
LIMIT 100
