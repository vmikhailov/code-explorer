MATCH (f:File)
RETURN f.name AS fileName, length(f.name) AS nameLength

