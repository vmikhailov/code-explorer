MATCH (p:Project)-[:CONTAINS]->(ep:Endpoint)
OPTIONAL MATCH (ep)-[:TRIGGERS]->(fn:Function)
OPTIONAL MATCH (fn)-[:CALLS*1..4]->(downstream)
WHERE downstream:ExternalService OR downstream:Endpoint OR downstream:Topic
RETURN p.name AS sourceProject,
       ep.name AS ingressEndpoint,
       coalesce(ep.protocol, 'REST') AS ingressProtocol,
       coalesce(ep.http_method, '') AS ingressMethod,
       coalesce(ep.route_template, ep.route, '') AS ingressRoute,
       coalesce(ep.request_type, '') AS requestType,
       coalesce(ep.response_type, '') AS responseType,
       coalesce(fn.name, '') AS handlerFunction,
       labels(downstream)[0] AS egressKind,
       coalesce(downstream.name, '') AS egressTarget
