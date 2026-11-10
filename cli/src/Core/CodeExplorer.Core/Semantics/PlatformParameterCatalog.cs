using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Semantics;

/// <summary>
/// Declarative catalog of known platform APIs, libraries, object option keys, and parameter annotations
/// mapping argument slots to their semantic nature.
/// </summary>
public static class PlatformParameterCatalog
{
    private const char DotSeparator = '.';
    private const char ParenOpen = '(';
    private const char AtPrefix = '@';
    private const char HashPrefix = '#';
    private const char BracketOpen = '[';
    private const char BracketClose = ']';

    private const string SqlToken = "sql";
    private const string QueryToken = "query";
    private const string StmtSuffix = "stmt";
    private const string ConnStrToken = "connstr";
    private const string ConnectionStringToken = "connectionstring";
    private const string UrlToken = "url";
    private const string UriToken = "uri";
    private const string EndpointToken = "endpoint";
    private const string RouteToken = "route";
    private const string FilePathToken = "filepath";
    private const string PathSuffix = "path";
    private const string FileNameToken = "filename";
    private const string TopicToken = "topic";
    private const string QueueToken = "queue";
    private const string RoutingKeyToken = "routingkey";
    private const string SecretToken = "secret";
    private const string ApiKeyToken = "apikey";
    private const string AuthHeaderToken = "authheader";
    private const string BearerToken = "bearer";
    private const string JsonAttrToken = "json";
    private const string RegexAttrToken = "regex";
    private const string LanguageInjectionToken = "languageinjection";
    private const string StringSyntaxToken = "stringsyntax";

    private static readonly Dictionary<string, (int ArgIndex, ParameterNature Nature)[]> MethodRegistry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // --- 1. Database / SQL Sinks ---
            // .NET (Dapper, EF Core, ADO.NET)
            ["Query"] = [(0, ParameterNature.SqlQuery)],
            ["QueryAsync"] = [(0, ParameterNature.SqlQuery)],
            ["QueryFirst"] = [(0, ParameterNature.SqlQuery)],
            ["QueryFirstAsync"] = [(0, ParameterNature.SqlQuery)],
            ["QueryFirstOrDefault"] = [(0, ParameterNature.SqlQuery)],
            ["QueryFirstOrDefaultAsync"] = [(0, ParameterNature.SqlQuery)],
            ["QuerySingle"] = [(0, ParameterNature.SqlQuery)],
            ["QuerySingleAsync"] = [(0, ParameterNature.SqlQuery)],
            ["QuerySingleOrDefault"] = [(0, ParameterNature.SqlQuery)],
            ["QuerySingleOrDefaultAsync"] = [(0, ParameterNature.SqlQuery)],
            ["QueryMultiple"] = [(0, ParameterNature.SqlQuery)],
            ["QueryMultipleAsync"] = [(0, ParameterNature.SqlQuery)],
            ["Execute"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteAsync"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteReader"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteReaderAsync"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteScalar"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteScalarAsync"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteNonQuery"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteNonQueryAsync"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteSqlRaw"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteSqlRawAsync"] = [(0, ParameterNature.SqlQuery)],
            ["FromSqlRaw"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteSqlInterpolated"] = [(0, ParameterNature.SqlQuery)],
            ["ExecuteSqlInterpolatedAsync"] = [(0, ParameterNature.SqlQuery)],
            ["FromSqlInterpolated"] = [(0, ParameterNature.SqlQuery)],
            ["FromSql"] = [(0, ParameterNature.SqlQuery)],
            ["SqlCommand"] = [(0, ParameterNature.SqlQuery)],
            ["NpgsqlCommand"] = [(0, ParameterNature.SqlQuery)],
            ["MySqlCommand"] = [(0, ParameterNature.SqlQuery)],
            ["SqliteCommand"] = [(0, ParameterNature.SqlQuery)],
            ["OracleCommand"] = [(0, ParameterNature.SqlQuery)],

            // JavaScript / TypeScript (pg, mysql2, knex, typeorm, sequelize, prisma, slonik)
            ["raw"] = [(0, ParameterNature.SqlQuery)],
            ["sql"] = [(0, ParameterNature.SqlQuery)],
            ["$queryRaw"] = [(0, ParameterNature.SqlQuery)],
            ["$queryRawUnsafe"] = [(0, ParameterNature.SqlQuery)],
            ["$executeRaw"] = [(0, ParameterNature.SqlQuery)],
            ["$executeRawUnsafe"] = [(0, ParameterNature.SqlQuery)],
            ["queryRaw"] = [(0, ParameterNature.SqlQuery)],
            ["executeRaw"] = [(0, ParameterNature.SqlQuery)],

            // GraphQL Sinks
            ["gql"] = [(0, ParameterNature.GraphQuery)],
            ["graphql"] = [(0, ParameterNature.GraphQuery)],

            // Python (sqlite3, psycopg2, sqlalchemy, asyncpg)
            ["executemany"] = [(0, ParameterNature.SqlQuery)],
            ["text"] = [(0, ParameterNature.SqlQuery)],

            // Go (database/sql, sqlx, gorm)
            ["QueryRow"] = [(0, ParameterNature.SqlQuery)],
            ["QueryContext"] = [(1, ParameterNature.SqlQuery)],
            ["QueryRowContext"] = [(1, ParameterNature.SqlQuery)],
            ["Exec"] = [(0, ParameterNature.SqlQuery)],
            ["ExecContext"] = [(1, ParameterNature.SqlQuery)],

            // Java (JDBC, Hibernate, JPA)
            ["prepareStatement"] = [(0, ParameterNature.SqlQuery)],
            ["prepareCall"] = [(0, ParameterNature.SqlQuery)],
            ["executeQuery"] = [(0, ParameterNature.SqlQuery)],
            ["executeUpdate"] = [(0, ParameterNature.SqlQuery)],
            ["createNativeQuery"] = [(0, ParameterNature.SqlQuery)],

            // --- 2. HTTP / URL Sinks ---
            ["GetAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["PostAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["PutAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["DeleteAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["PatchAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["GetStringAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["GetByteArrayAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["GetStreamAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["GetFromJsonAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["PostAsJsonAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["PutAsJsonAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["SendAsync"] = [(0, ParameterNature.UrlOrRoute)],
            ["fetch"] = [(0, ParameterNature.UrlOrRoute)],
            ["axios"] = [(0, ParameterNature.UrlOrRoute)],
            ["axios.get"] = [(0, ParameterNature.UrlOrRoute)],
            ["axios.post"] = [(0, ParameterNature.UrlOrRoute)],
            ["axios.put"] = [(0, ParameterNature.UrlOrRoute)],
            ["axios.delete"] = [(0, ParameterNature.UrlOrRoute)],
            ["axios.patch"] = [(0, ParameterNature.UrlOrRoute)],
            ["axios.request"] = [(0, ParameterNature.UrlOrRoute)],
            ["http.get"] = [(0, ParameterNature.UrlOrRoute)],
            ["http.post"] = [(0, ParameterNature.UrlOrRoute)],
            ["http.request"] = [(0, ParameterNature.UrlOrRoute)],
            ["https.get"] = [(0, ParameterNature.UrlOrRoute)],
            ["https.request"] = [(0, ParameterNature.UrlOrRoute)],
            ["NewRequest"] = [(1, ParameterNature.UrlOrRoute)],
            ["NewRequestWithContext"] = [(2, ParameterNature.UrlOrRoute)],

            // --- 3. Connection String Sinks ---
            ["SqlConnection"] = [(0, ParameterNature.ConnectionString)],
            ["NpgsqlConnection"] = [(0, ParameterNature.ConnectionString)],
            ["MySqlConnection"] = [(0, ParameterNature.ConnectionString)],
            ["SqliteConnection"] = [(0, ParameterNature.ConnectionString)],
            ["OracleConnection"] = [(0, ParameterNature.ConnectionString)],
            ["UseSqlServer"] = [(0, ParameterNature.ConnectionString)],
            ["UseNpgsql"] = [(0, ParameterNature.ConnectionString)],
            ["UseMySql"] = [(0, ParameterNature.ConnectionString)],
            ["UseSqlite"] = [(0, ParameterNature.ConnectionString)],
            ["createConnection"] = [(0, ParameterNature.ConnectionString)],
            ["createPool"] = [(0, ParameterNature.ConnectionString)],

            // --- 4. File Path Sinks ---
            ["ReadAllText"] = [(0, ParameterNature.FilePath)],
            ["ReadAllTextAsync"] = [(0, ParameterNature.FilePath)],
            ["ReadAllLines"] = [(0, ParameterNature.FilePath)],
            ["ReadAllBytes"] = [(0, ParameterNature.FilePath)],
            ["WriteAllText"] = [(0, ParameterNature.FilePath)],
            ["WriteAllTextAsync"] = [(0, ParameterNature.FilePath)],
            ["WriteAllLines"] = [(0, ParameterNature.FilePath)],
            ["WriteAllBytes"] = [(0, ParameterNature.FilePath)],
            ["OpenRead"] = [(0, ParameterNature.FilePath)],
            ["OpenWrite"] = [(0, ParameterNature.FilePath)],
            ["readFile"] = [(0, ParameterNature.FilePath)],
            ["readFileSync"] = [(0, ParameterNature.FilePath)],
            ["writeFile"] = [(0, ParameterNature.FilePath)],
            ["writeFileSync"] = [(0, ParameterNature.FilePath)],

            // --- 5. Command / Process Sinks ---
            ["Process.Start"] = [(0, ParameterNature.CommandOrScript)],
            ["execSync"] = [(0, ParameterNature.CommandOrScript)],
            ["spawn"] = [(0, ParameterNature.CommandOrScript)],
            ["spawnSync"] = [(0, ParameterNature.CommandOrScript)],
            ["Popen"] = [(0, ParameterNature.CommandOrScript)],
            ["subprocess.run"] = [(0, ParameterNature.CommandOrScript)],
            ["exec.Command"] = [(0, ParameterNature.CommandOrScript)],

            // --- 6. Topic / Queue Sinks ---
            ["PublishAsync"] = [(0, ParameterNature.TopicOrQueue)],
            ["ProduceAsync"] = [(0, ParameterNature.TopicOrQueue)],
            ["SendMessageAsync"] = [(0, ParameterNature.TopicOrQueue)],
            ["SendToQueue"] = [(0, ParameterNature.TopicOrQueue)],
            ["BasicPublish"] = [(0, ParameterNature.TopicOrQueue), (1, ParameterNature.TopicOrQueue)],
            ["publish"] = [(0, ParameterNature.TopicOrQueue)],
            ["sendToQueue"] = [(0, ParameterNature.TopicOrQueue)]
        };

    /// <summary>
    /// Unambiguous options-object property keys whose semantic nature holds across callees.
    /// </summary>
    private static readonly Dictionary<string, ParameterNature> UnambiguousObjectPropertyRoles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["sql"] = ParameterNature.SqlQuery,
            ["commandText"] = ParameterNature.SqlQuery,
            ["rawSql"] = ParameterNature.SqlQuery,
            ["url"] = ParameterNature.UrlOrRoute,
            ["uri"] = ParameterNature.UrlOrRoute,
            ["href"] = ParameterNature.UrlOrRoute,
            ["endpoint"] = ParameterNature.UrlOrRoute,
            ["baseURL"] = ParameterNature.UrlOrRoute,
            ["baseUrl"] = ParameterNature.UrlOrRoute,
            ["connectionString"] = ParameterNature.ConnectionString,
            ["dsn"] = ParameterNature.ConnectionString,
            ["topic"] = ParameterNature.TopicOrQueue,
            ["queue"] = ParameterNature.TopicOrQueue,
            ["routingKey"] = ParameterNature.TopicOrQueue,
            ["QueueUrl"] = ParameterNature.TopicOrQueue,
            ["TopicArn"] = ParameterNature.TopicOrQueue,
            ["headers"] = ParameterNature.SecretOrHeader,
            ["authorization"] = ParameterNature.SecretOrHeader,
            ["apiKey"] = ParameterNature.SecretOrHeader
        };

    private static readonly HashSet<string> SqlOptionCallees = new(StringComparer.OrdinalIgnoreCase)
    {
        "query", "execute", "raw", "prepare", "all", "get", "run", "one", "many", "none"
    };

    private static readonly HashSet<string> HttpOptionCallees = new(StringComparer.OrdinalIgnoreCase)
    {
        "request", "axios", "fetch", "get", "post", "put", "delete", "patch", "got", "ky"
    };

    /// <summary>
    /// Infers the semantic parameter nature for a call argument given the callee method and argument position.
    /// </summary>
    public static ParameterNature ResolveParameterNature(string? callee, int argIndex)
    {
        if (string.IsNullOrWhiteSpace(callee)) return ParameterNature.Unspecified;

        if (MethodRegistry.TryGetValue(callee, out var fullMappings))
        {
            for (var i = 0; i < fullMappings.Length; i++)
            {
                if (fullMappings[i].ArgIndex == argIndex)
                {
                    return fullMappings[i].Nature;
                }
            }
        }

        var methodName = ExtractShortMethodName(callee);
        if (!ReferenceEquals(methodName, callee) && MethodRegistry.TryGetValue(methodName, out var shortMappings))
        {
            for (var i = 0; i < shortMappings.Length; i++)
            {
                if (shortMappings[i].ArgIndex == argIndex)
                {
                    return shortMappings[i].Nature;
                }
            }
        }

        return ParameterNature.Unspecified;
    }

    /// <summary>
    /// Infers the semantic nature of a named property inside an options object passed to a call
    /// (e.g., <c>client.query({ text: 'SELECT ...' })</c> or <c>axios.request({ url: base + '/v1' })</c>).
    /// </summary>
    public static ParameterNature ResolveObjectPropertyNature(string? callee, string? propertyKey)
    {
        if (string.IsNullOrWhiteSpace(propertyKey)) return ParameterNature.Unspecified;

        if (UnambiguousObjectPropertyRoles.TryGetValue(propertyKey, out var directNature))
        {
            return directNature;
        }

        var shortCallee = ExtractShortMethodName(callee ?? string.Empty);

        // Context-sensitive object keys: avoid coloring UI `{ text: 'Click me' }` or SVG `{ path: 'M0 0' }`
        if (propertyKey.Equals("text", StringComparison.OrdinalIgnoreCase) ||
            propertyKey.Equals("query", StringComparison.OrdinalIgnoreCase) ||
            propertyKey.Equals("statement", StringComparison.OrdinalIgnoreCase))
        {
            if (SqlOptionCallees.Contains(shortCallee) ||
                ResolveParameterNature(callee, 0) == ParameterNature.SqlQuery)
            {
                return ParameterNature.SqlQuery;
            }

            if (propertyKey.Equals("query", StringComparison.OrdinalIgnoreCase) &&
                (shortCallee.Contains("graphql", StringComparison.OrdinalIgnoreCase) ||
                 shortCallee.Equals("gql", StringComparison.OrdinalIgnoreCase)))
            {
                return ParameterNature.GraphQuery;
            }
        }

        if (propertyKey.Equals("path", StringComparison.OrdinalIgnoreCase) ||
            propertyKey.Equals("pathname", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpOptionCallees.Contains(shortCallee) ||
                ResolveParameterNature(callee, 0) == ParameterNature.UrlOrRoute)
            {
                return ParameterNature.UrlOrRoute;
            }
        }

        if (propertyKey.Equals("auth", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpOptionCallees.Contains(shortCallee) ||
                ResolveParameterNature(callee, 0) == ParameterNature.UrlOrRoute)
            {
                return ParameterNature.SecretOrHeader;
            }
        }

        return ParameterNature.Unspecified;
    }

    /// <summary>
    /// Inspects parameter annotations/attributes (e.g., <c>[Sql]</c>, <c>[StringSyntax("Sql")]</c>, <c>[StringSyntax("Uri")]</c>)
    /// for explicit semantic coloring.
    /// </summary>
    public static ParameterNature ResolveAnnotationNature(IReadOnlyList<AnnotationSyntax>? annotations)
    {
        if (annotations == null || annotations.Count == 0) return ParameterNature.Unspecified;

        for (var i = 0; i < annotations.Count; i++)
        {
            var ann = annotations[i];
            var cleanName = ann.Name.Trim().TrimStart(AtPrefix, HashPrefix, BracketOpen).TrimEnd(BracketClose);
            var parenIdx = cleanName.IndexOf(ParenOpen);
            if (parenIdx > 0) cleanName = cleanName[..parenIdx].Trim();
            var shortAttr = ExtractShortMethodName(cleanName);

            if (shortAttr.Equals("Sql", StringComparison.OrdinalIgnoreCase) ||
                shortAttr.Equals("SqlAttribute", StringComparison.OrdinalIgnoreCase))
            {
                return ParameterNature.SqlQuery;
            }

            if (shortAttr.Contains(StringSyntaxToken, StringComparison.OrdinalIgnoreCase) ||
                shortAttr.Contains(LanguageInjectionToken, StringComparison.OrdinalIgnoreCase))
            {
                var fullText = ann.Text;
                if (fullText.Contains(SqlToken, StringComparison.OrdinalIgnoreCase))
                    return ParameterNature.SqlQuery;
                if (fullText.Contains(UriToken, StringComparison.OrdinalIgnoreCase) || fullText.Contains(UrlToken, StringComparison.OrdinalIgnoreCase))
                    return ParameterNature.UrlOrRoute;
                if (fullText.Contains(JsonAttrToken, StringComparison.OrdinalIgnoreCase))
                    return ParameterNature.JsonPayload;
                if (fullText.Contains(RegexAttrToken, StringComparison.OrdinalIgnoreCase))
                    return ParameterNature.RegexPattern;
            }
        }

        return ParameterNature.Unspecified;
    }

    /// <summary>
    /// Infers nature from parameter or variable naming heuristics when caller API is dynamic or unknown.
    /// </summary>
    public static ParameterNature ClassifyByName(string? identifierName)
    {
        if (string.IsNullOrWhiteSpace(identifierName)) return ParameterNature.Unspecified;

        var lower = identifierName.ToLowerInvariant();

        if (lower.Contains(SqlToken, StringComparison.Ordinal) ||
            lower.Contains(QueryToken, StringComparison.Ordinal) ||
            lower.EndsWith(StmtSuffix, StringComparison.Ordinal))
        {
            return ParameterNature.SqlQuery;
        }

        if (lower.Contains(ConnStrToken, StringComparison.Ordinal) ||
            lower.Contains(ConnectionStringToken, StringComparison.Ordinal))
        {
            return ParameterNature.ConnectionString;
        }

        if (lower.Contains(UrlToken, StringComparison.Ordinal) ||
            lower.Contains(UriToken, StringComparison.Ordinal) ||
            lower.Contains(EndpointToken, StringComparison.Ordinal) ||
            lower.Contains(RouteToken, StringComparison.Ordinal))
        {
            return ParameterNature.UrlOrRoute;
        }

        if (lower.Contains(FilePathToken, StringComparison.Ordinal) ||
            lower.EndsWith(PathSuffix, StringComparison.Ordinal) ||
            lower.Contains(FileNameToken, StringComparison.Ordinal))
        {
            return ParameterNature.FilePath;
        }

        if (lower.Contains(TopicToken, StringComparison.Ordinal) ||
            lower.Contains(QueueToken, StringComparison.Ordinal) ||
            lower.Contains(RoutingKeyToken, StringComparison.Ordinal))
        {
            return ParameterNature.TopicOrQueue;
        }

        if (lower.Contains(SecretToken, StringComparison.Ordinal) ||
            lower.Contains(ApiKeyToken, StringComparison.Ordinal) ||
            lower.Contains(AuthHeaderToken, StringComparison.Ordinal) ||
            lower.Contains(BearerToken, StringComparison.Ordinal))
        {
            return ParameterNature.SecretOrHeader;
        }

        return ParameterNature.Unspecified;
    }

    public static string ExtractShortMethodName(string callee)
    {
        var dotIndex = callee.LastIndexOf(DotSeparator);
        return dotIndex >= 0 && dotIndex < callee.Length - 1
            ? callee[(dotIndex + 1)..]
            : callee;
    }
}
