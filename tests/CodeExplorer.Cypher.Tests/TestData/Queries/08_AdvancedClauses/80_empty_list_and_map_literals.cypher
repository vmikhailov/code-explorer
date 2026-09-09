MATCH (n)
WHERE n.tags = [] OR n.props = {}
RETURN count(n) AS cnt
