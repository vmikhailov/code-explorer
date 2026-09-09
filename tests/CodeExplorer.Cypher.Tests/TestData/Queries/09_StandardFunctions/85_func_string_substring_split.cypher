MATCH (fn:Function)
RETURN substring(fn.name, 0, 4) AS prefix, split(fn.name, '_') AS parts
