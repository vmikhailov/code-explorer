MATCH (t:Type)
RETURN toLower(t.name) AS lowerName, toUpper(t.kind) AS upperKind

