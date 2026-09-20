MATCH (w:Workspace), (f:File)
RETURN w.name AS wsName, f.name AS fileName

