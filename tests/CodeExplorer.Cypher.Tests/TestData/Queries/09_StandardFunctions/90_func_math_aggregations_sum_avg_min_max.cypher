MATCH (fn:Function)
RETURN sum(fn.start_line) AS totalLines, avg(fn.start_line) AS avgLine, min(fn.start_line) AS minLine, max(fn.start_line) AS maxLine
