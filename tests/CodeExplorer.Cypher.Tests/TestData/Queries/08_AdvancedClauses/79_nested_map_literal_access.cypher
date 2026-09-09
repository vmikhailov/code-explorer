MATCH (p:Project)
RETURN { meta: { env: 'prod', version: 2 } } AS config
