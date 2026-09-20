MATCH (p:Project)
RETURN p { .name, custom: 'fixed' } AS projectMap
