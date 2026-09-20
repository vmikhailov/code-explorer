MATCH (fn:Function)
RETURN fn.name AS functionName, fn.end_line - fn.start_line AS duration
