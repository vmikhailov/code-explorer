MATCH (n:File)
RETURN n.name AS fileName, 'source_file' AS nodeCategory, 100 AS priority, true AS isIndexed

