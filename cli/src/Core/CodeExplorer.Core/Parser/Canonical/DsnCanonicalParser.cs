using System.Text;

namespace CodeExplorer.Core.Parser.Canonical;

public sealed record CanonicalDsn(
    string RawDsn,
    string CanonicalTemplate,
    string CanonicalShape,
    string Engine,
    string Host,
    int? Port,
    string Database,
    string? Username = null);

/// <summary>
/// Zero-regex, bijectively reversible canonical database/cache connection string (DSN) sub-parser.
/// Normalizes URI (postgresql://user@host:5432/db), JDBC (jdbc:postgresql://host:5432/db), and
/// ADO.NET (Host=host;Port=5432;Database=db;Username=user) connection strings into a single canonical grammar.
/// </summary>
public static class DsnCanonicalParser
{
    private const char SlashChar = '/';
    private const char ColonChar = ':';
    private const char SemicolonChar = ';';
    private const char EqualsChar = '=';
    private const char AtChar = '@';
    private const char QuestionChar = '?';
    private const string CanonicalPrefix = "dsn://";
    private const string CanonicalShapePrefix = "db:";
    private const string JdbcPrefix = "jdbc:";
    private const string SchemeSeparator = "://";
    private const string DefaultEngine = "postgresql";
    private const string DefaultHost = "localhost";

    public static CanonicalDsn Parse(string? rawDsn, DsnDialect dialect = DsnDialect.Auto, string? defaultEngine = null)
    {
        if (string.IsNullOrWhiteSpace(rawDsn))
        {
            return new CanonicalDsn(rawDsn ?? string.Empty, CanonicalPrefix, CanonicalShapePrefix, defaultEngine ?? DefaultEngine, DefaultHost, null, string.Empty);
        }

        var span = rawDsn.AsSpan().Trim();

        // 1. Canonical format: dsn://{engine}/[{user}@]{host}[:{port}]/{database}
        if (span.StartsWith(CanonicalPrefix.AsSpan(), StringComparison.Ordinal))
        {
            var body = span[CanonicalPrefix.Length..];
            var slash1 = body.IndexOf(SlashChar);
            if (slash1 > 0)
            {
                var engine = NormalizeEngine(body[..slash1].ToString());
                return ParseAuthorityAndDb(rawDsn, engine, body[(slash1 + 1)..]);
            }
        }

        // 2. ADO.NET key-value format: Host=...;Port=...;Database=...;Username=...
        if (dialect == DsnDialect.AdoNet || (span.IndexOf(EqualsChar) > 0 && span.IndexOf(SchemeSeparator.AsSpan(), StringComparison.Ordinal) < 0))
        {
            return ParseAdoNet(rawDsn, span, defaultEngine ?? DefaultEngine);
        }

        // 3. JDBC or URI format: [jdbc:]{engine}://[{user}@]{host}[:{port}]/{database}
        if (span.StartsWith(JdbcPrefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            span = span[JdbcPrefix.Length..];
        }

        var schemeIdx = span.IndexOf(SchemeSeparator.AsSpan(), StringComparison.Ordinal);
        if (schemeIdx > 0)
        {
            var engine = NormalizeEngine(span[..schemeIdx].ToString());
            var rest = span[(schemeIdx + SchemeSeparator.Length)..];
            return ParseAuthorityAndDb(rawDsn, engine, rest);
        }

        var fallbackEngine = NormalizeEngine(defaultEngine ?? DefaultEngine);
        return new CanonicalDsn(
            rawDsn,
            $"{CanonicalPrefix}{fallbackEngine}/{DefaultHost}/{span.ToString()}",
            $"{CanonicalShapePrefix}{fallbackEngine}://{DefaultHost}/{span.ToString().ToLowerInvariant()}",
            fallbackEngine,
            DefaultHost,
            null,
            span.ToString());
    }

    public static string ToCanonical(string rawDsn, DsnDialect dialect = DsnDialect.Auto, string? defaultEngine = null) =>
        Parse(rawDsn, dialect, defaultEngine).CanonicalTemplate;

    public static string ToCanonicalShape(string rawDsn, DsnDialect dialect = DsnDialect.Auto, string? defaultEngine = null) =>
        Parse(rawDsn, dialect, defaultEngine).CanonicalShape;

    public static string FromCanonical(string canonicalDsn, DsnDialect targetDialect) =>
        Format(Parse(canonicalDsn, DsnDialect.Canonical), targetDialect);

    public static string Format(CanonicalDsn dsn, DsnDialect targetDialect)
    {
        var portPart = dsn.Port.HasValue ? $":{dsn.Port.Value}" : string.Empty;
        var userPart = !string.IsNullOrEmpty(dsn.Username) ? $"{dsn.Username}@" : string.Empty;

        return targetDialect switch
        {
            DsnDialect.Uri =>
                $"{dsn.Engine}://{userPart}{dsn.Host}{portPart}/{dsn.Database}",
            DsnDialect.Jdbc =>
                $"jdbc:{dsn.Engine}://{userPart}{dsn.Host}{portPart}/{dsn.Database}",
            DsnDialect.AdoNet =>
                FormatAdoNet(dsn),
            _ =>
                dsn.CanonicalTemplate
        };
    }

    private static string FormatAdoNet(CanonicalDsn dsn)
    {
        var sb = new StringBuilder(64);
        sb.Append("Host=").Append(dsn.Host);
        if (dsn.Port.HasValue)
        {
            sb.Append(";Port=").Append(dsn.Port.Value);
        }
        sb.Append(";Database=").Append(dsn.Database);
        if (!string.IsNullOrEmpty(dsn.Username))
        {
            sb.Append(";Username=").Append(dsn.Username);
        }
        return sb.ToString();
    }

    private static CanonicalDsn ParseAuthorityAndDb(string rawDsn, string engine, ReadOnlySpan<char> rest)
    {
        var qIdx = rest.IndexOf(QuestionChar);
        if (qIdx >= 0) rest = rest[..qIdx];

        var slashIdx = rest.IndexOf(SlashChar);
        var authority = slashIdx >= 0 ? rest[..slashIdx] : rest;
        var dbSpan = slashIdx >= 0 ? rest[(slashIdx + 1)..] : ReadOnlySpan<char>.Empty;

        string? username = null;
        var atIdx = authority.IndexOf(AtChar);
        if (atIdx >= 0)
        {
            var userInfo = authority[..atIdx];
            var colonUser = userInfo.IndexOf(ColonChar);
            username = (colonUser >= 0 ? userInfo[..colonUser] : userInfo).ToString();
            authority = authority[(atIdx + 1)..];
        }

        int? port = null;
        string host;
        var colonPort = authority.LastIndexOf(ColonChar);
        if (colonPort > 0 && int.TryParse(authority[(colonPort + 1)..], out var parsedPort))
        {
            host = authority[..colonPort].ToString();
            port = parsedPort;
        }
        else
        {
            host = authority.IsEmpty ? DefaultHost : authority.ToString();
        }

        var database = dbSpan.ToString();
        return BuildCanonicalDsn(rawDsn, engine, host, port, database, username);
    }

    private static CanonicalDsn ParseAdoNet(string rawDsn, ReadOnlySpan<char> span, string defaultEngine)
    {
        var host = DefaultHost;
        int? port = null;
        var database = string.Empty;
        string? username = null;
        var engine = NormalizeEngine(defaultEngine);

        var start = 0;
        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == SemicolonChar)
            {
                var pair = span[start..i].Trim();
                var eq = pair.IndexOf(EqualsChar);
                if (eq > 0)
                {
                    var key = pair[..eq].Trim();
                    var val = pair[(eq + 1)..].Trim();
                    if (key.Equals("Host".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("Server".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("Data Source".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        var colonIdx = val.LastIndexOf(ColonChar);
                        if (colonIdx > 0 && int.TryParse(val[(colonIdx + 1)..], out var p))
                        {
                            host = val[..colonIdx].ToString();
                            port = p;
                        }
                        else
                        {
                            host = val.ToString();
                        }
                    }
                    else if (key.Equals("Port".AsSpan(), StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out var p))
                    {
                        port = p;
                    }
                    else if (key.Equals("Database".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("Initial Catalog".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        database = val.ToString();
                    }
                    else if (key.Equals("Username".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("User Id".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("Uid".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        username = val.ToString();
                    }
                }
                start = i + 1;
            }
        }

        return BuildCanonicalDsn(rawDsn, engine, host, port, database, username);
    }

    private static CanonicalDsn BuildCanonicalDsn(
        string rawDsn,
        string engine,
        string host,
        int? port,
        string database,
        string? username)
    {
        var portPart = port.HasValue ? $":{port.Value}" : string.Empty;
        var userPart = !string.IsNullOrEmpty(username) ? $"{username}@" : string.Empty;
        var canonicalTemplate = $"{CanonicalPrefix}{engine}/{userPart}{host}{portPart}/{database}";
        var canonicalShape = $"{CanonicalShapePrefix}{engine}://{host.ToLowerInvariant()}{portPart}/{database.ToLowerInvariant()}";

        return new CanonicalDsn(rawDsn, canonicalTemplate, canonicalShape, engine, host, port, database, username);
    }

    private static string NormalizeEngine(string rawEngine) => rawEngine.Trim().ToLowerInvariant() switch
    {
        "postgres" or "postgresql" or "pgsql" => "postgresql",
        "mssql" or "sqlserver" => "sqlserver",
        "mongo" or "mongodb" or "mongodb+srv" => "mongodb",
        _ => rawEngine.Trim().ToLowerInvariant()
    };
}
