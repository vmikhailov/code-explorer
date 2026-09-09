MATCH (fn:Function)
RETURN fn.name AS functionName, fn.start_line ^ 2 AS squaredLine
