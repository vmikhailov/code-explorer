MATCH (fn:Function)
RETURN fn.name AS functionName, CASE WHEN fn.start_line > 30 THEN 'High' WHEN fn.start_line > 10 THEN 'Medium' ELSE 'Low' END AS complexity

