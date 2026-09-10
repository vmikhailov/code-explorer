MATCH (w:Workspace) WHERE toLower(w.path) = toLower($path) OR toLower(w.path) = toLower($altPath) RETURN w.id AS id LIMIT 1
