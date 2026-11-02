MATCH (ep:Endpoint)-[:DECLARED_IN]->(p:Project)
OPTIONAL MATCH (ep)-[:TRIGGERS]->(fn:Function)
OPTIONAL MATCH (fn)-[:CALLS*0..3]->(caller:Function)
OPTIONAL MATCH (downstream)-[:CALLED_BY|PUBLISHED_BY]->(caller)
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
