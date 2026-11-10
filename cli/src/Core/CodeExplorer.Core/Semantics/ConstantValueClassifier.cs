namespace CodeExplorer.Core.Semantics;

/// <summary>
/// Classifies resolved argument constant or template values into fine-grained semantic categories.
/// </summary>
public static class ConstantValueClassifier
{
    private const char SlashChar = '/';
    private const char ColonChar = ':';
    private const char BraceOpen = '{';
    private const char SemicolonChar = ';';
    private const char EqualsChar = '=';
    private const char NewlineChar = '\n';

    private const string LineCommentPrefix = "--";
    private const string BlockCommentOpen = "/*";
    private const string BlockCommentClose = "*/";

    private const string HttpScheme = "http://";
    private const string HttpsScheme = "https://";
    private const string WsScheme = "ws://";
    private const string WssScheme = "wss://";
    private const string GrpcScheme = "grpc://";
    private const string LocalhostToken = "localhost";
    private const string LoopbackIpv4 = "127.0.0.1";
    private const string LoopbackIpv6 = "[::1]";
    private const string ApiPrefix = "api/";
    private const string VersionPrefix = "v1/";

    private static readonly string[] SqlDmlKeywords =
        ["SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "WITH", "VALUES", "REPLACE"];

    private static readonly string[] SqlDdlKeywords =
        ["CREATE", "ALTER", "DROP", "TRUNCATE", "RENAME"];

    private static readonly string[] SqlExecKeywords =
        ["EXEC", "EXECUTE", "CALL"];

    private static readonly string[] ConnStrIndicators =
    [
        "Server=", "Host=", "Data Source=", "Database=", "Initial Catalog=",
        "postgres://", "postgresql://", "mysql://", "mssql://", "sqlserver://",
        "mongodb://", "mongodb+srv://", "redis://", "rediss://", "amqp://"
    ];

    public static ConstantValueKind Classify(ParameterNature nature, string? resolvedValue)
    {
        if (string.IsNullOrWhiteSpace(resolvedValue))
        {
            return ConstantValueKind.Unknown;
        }

        var trimmed = resolvedValue.Trim();

        switch (nature)
        {
            case ParameterNature.SqlQuery:
                return ClassifySqlValue(trimmed);

            case ParameterNature.UrlOrRoute:
                return ClassifyUrlOrRouteValue(trimmed);

            case ParameterNature.ConnectionString:
                return ClassifyConnectionStringValue(trimmed);

            case ParameterNature.SecretOrHeader:
                return ConstantValueKind.SecretLiteral;

            case ParameterNature.TopicOrQueue:
                return ConstantValueKind.TopicLiteral;

            case ParameterNature.FilePath:
                return ConstantValueKind.FilePathLiteral;

            case ParameterNature.CommandOrScript:
                return ConstantValueKind.CommandLiteral;

            default:
                return ConstantValueKind.Unknown;
        }
    }

    private static ConstantValueKind ClassifySqlValue(string sql)
    {
        var span = StripLeadingSqlComments(sql.AsSpan());
        if (span.IsEmpty) return ConstantValueKind.Unknown;

        var wordLen = 0;
        while (wordLen < span.Length && char.IsLetter(span[wordLen]))
        {
            wordLen++;
        }

        if (wordLen == 0) return ConstantValueKind.Unknown;
        var firstWord = span[..wordLen];

        for (var i = 0; i < SqlDmlKeywords.Length; i++)
        {
            if (firstWord.Equals(SqlDmlKeywords[i], StringComparison.OrdinalIgnoreCase))
                return ConstantValueKind.SqlDml;
        }

        for (var i = 0; i < SqlDdlKeywords.Length; i++)
        {
            if (firstWord.Equals(SqlDdlKeywords[i], StringComparison.OrdinalIgnoreCase))
                return ConstantValueKind.SqlDdl;
        }

        for (var i = 0; i < SqlExecKeywords.Length; i++)
        {
            if (firstWord.Equals(SqlExecKeywords[i], StringComparison.OrdinalIgnoreCase))
                return ConstantValueKind.SqlExec;
        }

        // Stored procedure name passed directly to Execute / CommandType.StoredProcedure (e.g. "dbo.usp_UpdateInventory")
        if (!span.Contains(' ') && span.Length >= 3)
        {
            return ConstantValueKind.SqlExec;
        }

        return ConstantValueKind.Unknown;
    }

    private static ReadOnlySpan<char> StripLeadingSqlComments(ReadOnlySpan<char> span)
    {
        while (!span.IsEmpty)
        {
            span = span.TrimStart();
            if (span.StartsWith(LineCommentPrefix, StringComparison.Ordinal))
            {
                var nl = span.IndexOf(NewlineChar);
                if (nl < 0) return ReadOnlySpan<char>.Empty;
                span = span[(nl + 1)..];
                continue;
            }

            if (span.StartsWith(BlockCommentOpen, StringComparison.Ordinal))
            {
                var closeIdx = span.IndexOf(BlockCommentClose, StringComparison.Ordinal);
                if (closeIdx < 0) return ReadOnlySpan<char>.Empty;
                span = span[(closeIdx + 2)..];
                continue;
            }

            break;
        }

        return span;
    }

    private static ConstantValueKind ClassifyUrlOrRouteValue(string url)
    {
        if (url.StartsWith(HttpScheme, StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith(HttpsScheme, StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith(WsScheme, StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith(WssScheme, StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith(GrpcScheme, StringComparison.OrdinalIgnoreCase))
        {
            if (url.Contains(LocalhostToken, StringComparison.OrdinalIgnoreCase) ||
                url.Contains(LoopbackIpv4, StringComparison.Ordinal) ||
                url.Contains(LoopbackIpv6, StringComparison.Ordinal))
            {
                return ConstantValueKind.UrlLoopback;
            }

            return ConstantValueKind.UrlPublicDomain;
        }

        if (url[0] == SlashChar ||
            url.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase) ||
            url.IndexOf(BraceOpen) >= 0 ||
            (url.IndexOf(SlashChar) >= 0 && url.IndexOf(ColonChar) >= 0))
        {
            return ConstantValueKind.UrlRelativeRoute;
        }

        return ConstantValueKind.Unknown;
    }

    private static ConstantValueKind ClassifyConnectionStringValue(string value)
    {
        for (var i = 0; i < ConnStrIndicators.Length; i++)
        {
            if (value.Contains(ConnStrIndicators[i], StringComparison.OrdinalIgnoreCase))
            {
                return ConstantValueKind.ConnStrRelational;
            }
        }

        if (value.IndexOf(EqualsChar) > 0 && value.IndexOf(SemicolonChar) > 0)
        {
            return ConstantValueKind.ConnStrRelational;
        }

        return ConstantValueKind.Unknown;
    }
}
