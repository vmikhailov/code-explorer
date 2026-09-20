MATCH (fn:Function)
RETURN fn.name AS functionName, (fn.start_line * 10) / 2 AS calc
