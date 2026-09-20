MATCH (n:Function)
RETURN ['first', 'middle', 'last'][-1] AS lastItem
