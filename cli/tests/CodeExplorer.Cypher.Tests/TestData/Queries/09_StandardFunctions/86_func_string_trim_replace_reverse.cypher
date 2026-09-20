MATCH (fn:Function)
RETURN trim('  test  ') AS trimmed, replace(fn.name, 'Old', 'New') AS replaced, reverse(fn.name) AS rev
