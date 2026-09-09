MATCH (n:Function)
RETURN toInteger('123') AS iVal, toFloat('3.14') AS fVal, toBoolean('true') AS bVal
