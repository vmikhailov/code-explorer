MATCH (t:Type)
RETURN t.name AS typeName, CASE t.kind WHEN 'class' THEN 'Concrete' WHEN 'interface' THEN 'Abstract' ELSE 'Other' END AS category

