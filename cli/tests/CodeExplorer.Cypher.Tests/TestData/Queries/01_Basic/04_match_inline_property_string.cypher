MATCH (t:Type {kind: 'class'})
RETURN t.name AS typeName, t.symbol AS symbol

