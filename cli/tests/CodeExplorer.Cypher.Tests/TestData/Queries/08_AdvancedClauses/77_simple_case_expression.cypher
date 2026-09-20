MATCH (n:Type)
RETURN n.name AS name, CASE n.kind WHEN 'class' THEN 'ClassDef' WHEN 'interface' THEN 'InterfaceDef' ELSE 'Other' END AS displayKind
