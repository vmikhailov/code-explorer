MATCH (n:Function)
RETURN head(['a', 'b', 'c']) AS firstItem, last(['a', 'b', 'c']) AS lastItem, tail(['a', 'b', 'c']) AS restItems
