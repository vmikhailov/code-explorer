MATCH (t:Type)
RETURN toUpper(toLower(t.name)) AS normalizedName

