using System.Text;

namespace CodeExplorer.Cypher.Compiler;

public record SqlQueryModel
{
    public IReadOnlyList<string> Ctes { get; init; } = [];
    public bool IsDistinct { get; init; }
    public required IReadOnlyList<string> SelectColumns { get; init; }
    public string? FromAndJoins { get; init; }
    public IReadOnlyList<string> WhereConditions { get; init; } = [];
    public IReadOnlyList<string> GroupByColumns { get; init; } = [];
    public IReadOnlyList<string> HavingConditions { get; init; } = [];
    public IReadOnlyList<string> OrderByItems { get; init; } = [];
    public string? Limit { get; init; }
    public string? Offset { get; init; }
}

public static class SqlTemplates
{
    public static string RenderQuery(SqlQueryModel m)
    {
        var sb = new StringBuilder();

        if (m.Ctes.Count > 0)
        {
            sb.Append("WITH RECURSIVE ");
            sb.Append(string.Join(",\n", m.Ctes));
            sb.AppendLine();
        }

        var distinctStr = m.IsDistinct ? "DISTINCT " : "";
        sb.Append("SELECT ").Append(distinctStr).Append(string.Join(", ", m.SelectColumns));

        if (!string.IsNullOrEmpty(m.FromAndJoins))
        {
            sb.AppendLine();
            sb.Append(m.FromAndJoins.Trim('\r', '\n'));
        }

        if (m.WhereConditions.Count > 0)
        {
            sb.AppendLine();
            sb.Append("WHERE ").Append(string.Join(" AND ", m.WhereConditions));
        }

        if (m.GroupByColumns.Count > 0)
        {
            sb.AppendLine();
            sb.Append("GROUP BY ").Append(string.Join(", ", m.GroupByColumns.Distinct()));
        }

        if (m.HavingConditions.Count > 0)
        {
            sb.AppendLine();
            sb.Append("HAVING ").Append(string.Join(" AND ", m.HavingConditions));
        }

        if (m.OrderByItems.Count > 0)
        {
            sb.AppendLine();
            sb.Append("ORDER BY ").Append(string.Join(", ", m.OrderByItems));
        }

        if (!string.IsNullOrEmpty(m.Limit))
        {
            sb.AppendLine();
            sb.Append($"LIMIT {m.Limit}");
        }

        if (!string.IsNullOrEmpty(m.Offset))
        {
            sb.AppendLine();
            sb.Append($"OFFSET {m.Offset}");
        }

        return sb.ToString().TrimEnd();
    }

    public static string RenderCollectSubquery(bool isDistinct, string projection, string fromJoins, string? whereSql)
    {
        var dist = isDistinct ? "DISTINCT " : "";
        var whereClause = !string.IsNullOrEmpty(whereSql) ? $" WHERE {whereSql}" : "";
        return $"(SELECT json_group_array({dist}{projection}) FILTER (WHERE {projection} IS NOT NULL) FROM {fromJoins}{whereClause})";
    }

    public static string RenderCountSubquery(bool isDistinct, string target, string fromJoins, string? whereSql)
    {
        var dist = isDistinct ? "DISTINCT " : "";
        var whereClause = !string.IsNullOrEmpty(whereSql) ? $" WHERE {whereSql}" : "";
        return $"(SELECT COUNT({dist}{target}) FROM {fromJoins}{whereClause})";
    }

    public static string RenderExistsSubquery(string fromJoins, string? whereSql)
    {
        var whereClause = !string.IsNullOrEmpty(whereSql) ? $" WHERE {whereSql}" : "";
        return $"(EXISTS (SELECT 1 FROM {fromJoins}{whereClause}))";
    }

    public static string RenderQuantifier(string quantifier, string safeListSql, string variable, string predicateSql)
    {
        return quantifier switch
        {
            "any" => $"(EXISTS (SELECT 1 FROM json_each({safeListSql}) AS {variable} WHERE {predicateSql}))",
            "none" => $"(NOT EXISTS (SELECT 1 FROM json_each({safeListSql}) AS {variable} WHERE {predicateSql}))",
            "all" => $"(NOT EXISTS (SELECT 1 FROM json_each({safeListSql}) AS {variable} WHERE NOT ({predicateSql})))",
            "single" => $"((SELECT COUNT(1) FROM json_each({safeListSql}) AS {variable} WHERE {predicateSql}) = 1)",
            _ => $"(EXISTS (SELECT 1 FROM json_each({safeListSql}) AS {variable} WHERE {predicateSql}))"
        };
    }

    public static string RenderListComprehension(string safeListSql, string variable, string? filterSql, string projSql)
    {
        var whereClause = !string.IsNullOrEmpty(filterSql) ? $" WHERE {filterSql}" : "";
        return $"(SELECT json_group_array({projSql}) FROM json_each({safeListSql}) AS {variable}{whereClause})";
    }

    public static string RenderCollectedArray(bool isDistinct, string projSql, string? filterSql)
    {
        var dist = isDistinct ? "DISTINCT " : "";
        var filterClause = !string.IsNullOrEmpty(filterSql) ? $" FILTER (WHERE {filterSql})" : "";
        return $"json_group_array({dist}{projSql}){filterClause}";
    }
}
