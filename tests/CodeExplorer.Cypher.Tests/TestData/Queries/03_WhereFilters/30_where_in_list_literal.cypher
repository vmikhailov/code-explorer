MATCH (fn:Function)
WHERE fn.name IN ['ProcessOrder', 'SaveToDb']
RETURN fn.name AS functionName

