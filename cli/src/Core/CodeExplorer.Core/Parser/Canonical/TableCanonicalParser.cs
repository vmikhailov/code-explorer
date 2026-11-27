using System.Text;

namespace CodeExplorer.Core.Parser.Canonical;

public sealed record CanonicalTable(
    string RawTable,
    string CanonicalTemplate,
    string CanonicalShape,
    string TableName,
    string? Schema = null,
    string? Catalog = null);

/// <summary>
/// Zero-regex, bijectively reversible canonical SQL/ORM table identifier sub-parser.
/// Normalizes dialect quoting ([dbo].[UserOrders], "public"."user_orders", `billing`.`user_orders`)
/// into a single canonical representation (table:schema.table) while preserving exact table names.
/// </summary>
public static class TableCanonicalParser
{
    private const char DotChar = '.';
    private const char OpenBracketChar = '[';
    private const char CloseBracketChar = ']';
    private const char DoubleQuoteChar = '"';
    private const char BacktickChar = '`';
    private const string CanonicalPrefix = "table:";

    public static CanonicalTable Parse(
        string? rawTable,
        TableDialect dialect = TableDialect.Auto,
        string? explicitSchema = null)
    {
        if (string.IsNullOrWhiteSpace(rawTable))
        {
            return new CanonicalTable(rawTable ?? string.Empty, CanonicalPrefix, CanonicalPrefix, string.Empty);
        }

        var span = rawTable.AsSpan().Trim();
        if (span.StartsWith(CanonicalPrefix.AsSpan(), StringComparison.Ordinal))
        {
            span = span[CanonicalPrefix.Length..];
        }

        var parts = SplitQualifiedParts(span);
        string? catalog = null;
        var schema = explicitSchema;
        string tableName;

        if (parts.Count >= 3)
        {
            catalog = parts[0];
            schema = parts[1];
            tableName = parts[2];
        }
        else if (parts.Count == 2)
        {
            schema = parts[0];
            tableName = parts[1];
        }
        else if (parts.Count == 1)
        {
            tableName = parts[0];
        }
        else
        {
            tableName = span.ToString();
        }

        var sb = new StringBuilder(CanonicalPrefix.Length + tableName.Length + 16);
        sb.Append(CanonicalPrefix);
        if (!string.IsNullOrEmpty(catalog))
        {
            sb.Append(catalog).Append(DotChar);
        }
        if (!string.IsNullOrEmpty(schema))
        {
            sb.Append(schema).Append(DotChar);
        }
        sb.Append(tableName);

        var canonicalTemplate = sb.ToString();
        var canonicalShape = CanonicalPrefix + tableName.ToLowerInvariant();

        return new CanonicalTable(rawTable, canonicalTemplate, canonicalShape, tableName, schema, catalog);
    }

    public static string ToCanonical(string rawTable, TableDialect dialect = TableDialect.Auto, string? explicitSchema = null) =>
        Parse(rawTable, dialect, explicitSchema).CanonicalTemplate;

    public static string ToCanonicalShape(string rawTable, string? explicitSchema = null) =>
        Parse(rawTable, TableDialect.Auto, explicitSchema).CanonicalShape;

    public static string FromCanonical(string canonicalTable, TableDialect targetDialect) =>
        Format(Parse(canonicalTable, TableDialect.Canonical), targetDialect);

    public static string Format(CanonicalTable table, TableDialect targetDialect)
    {
        if (targetDialect is TableDialect.Canonical or TableDialect.Auto)
        {
            return table.CanonicalTemplate;
        }

        var sb = new StringBuilder(table.CanonicalTemplate.Length + 8);
        if (!string.IsNullOrEmpty(table.Catalog))
        {
            AppendDialectPart(sb, table.Catalog, targetDialect);
            sb.Append(DotChar);
        }
        if (!string.IsNullOrEmpty(table.Schema))
        {
            AppendDialectPart(sb, table.Schema, targetDialect);
            sb.Append(DotChar);
        }
        AppendDialectPart(sb, table.TableName, targetDialect);
        return sb.ToString();
    }

    private static void AppendDialectPart(StringBuilder sb, string part, TableDialect dialect)
    {
        switch (dialect)
        {
            case TableDialect.TSqlBracket:
                sb.Append(OpenBracketChar).Append(part).Append(CloseBracketChar);
                break;
            case TableDialect.AnsiDoubleQuote:
                sb.Append(DoubleQuoteChar).Append(part).Append(DoubleQuoteChar);
                break;
            case TableDialect.MySqlBacktick:
                sb.Append(BacktickChar).Append(part).Append(BacktickChar);
                break;
            default:
                sb.Append(part);
                break;
        }
    }

    private static List<string> SplitQualifiedParts(ReadOnlySpan<char> span)
    {
        var result = new List<string>(3);
        var start = 0;
        var inBracket = false;
        var inQuote = false;
        var inBacktick = false;

        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || (span[i] == DotChar && !inBracket && !inQuote && !inBacktick))
            {
                var part = UnquotePart(span[start..i].Trim());
                if (!part.IsEmpty)
                {
                    result.Add(part.ToString());
                }
                start = i + 1;
                continue;
            }

            var c = span[i];
            if (c == OpenBracketChar && !inQuote && !inBacktick) inBracket = true;
            else if (c == CloseBracketChar && inBracket) inBracket = false;
            else if (c == DoubleQuoteChar && !inBracket && !inBacktick) inQuote = !inQuote;
            else if (c == BacktickChar && !inBracket && !inQuote) inBacktick = !inBacktick;
        }

        return result;
    }

    private static ReadOnlySpan<char> UnquotePart(ReadOnlySpan<char> part)
    {
        if (part.Length >= 2)
        {
            if ((part[0] == OpenBracketChar && part[^1] == CloseBracketChar) ||
                (part[0] == DoubleQuoteChar && part[^1] == DoubleQuoteChar) ||
                (part[0] == BacktickChar && part[^1] == BacktickChar))
            {
                return part[1..^1];
            }
        }
        return part;
    }
}
