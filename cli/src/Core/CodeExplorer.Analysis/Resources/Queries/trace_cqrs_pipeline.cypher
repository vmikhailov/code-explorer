MATCH (topic:Topic)-[:PUBLISHED_BY]->(pub), (topic)-[:SUBSCRIBED_BY]->(sub)
RETURN 
    pub.name AS producer,
    labels(pub) AS producer_types,
    topic.name AS message,
    topic.broker_type AS broker,
    sub.name AS consumer,
    labels(sub) AS consumer_types
ORDER BY topic.name, pub.name

