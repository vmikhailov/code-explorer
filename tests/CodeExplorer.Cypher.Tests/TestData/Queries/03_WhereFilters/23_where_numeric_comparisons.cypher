MATCH (fn:Function)
WHERE fn.start_line >= 10 AND fn.end_line <= 50
RETURN fn.name AS functionName

