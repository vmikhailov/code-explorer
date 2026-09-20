MATCH (f:File)<-[:CONTAINS]-(w:Workspace)
RETURN f.name AS fileName, w.name AS wsName

