using System.Linq;
using CodeExplorer.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public interface ILibraryParser
{
    /// <summary>
    /// The friendly name of the parser (e.g., "MongooseLibraryParser").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The category of behavior this library addresses (e.g., "database", "api").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// The canonical library/package names that trigger this parser (e.g., ["mongoose", "mongodb"]).
    /// </summary>
    IEnumerable<string> SupportedLibraries { get; }

    /// <summary>
    /// Gets a value indicating whether this library parser is implemented.
    /// Defaults to false.
    /// </summary>
    bool IsImplemented => false;

    /// <summary>
    /// For database category, the engine name (e.g. "PostgreSQL", "MongoDB").
    /// </summary>
    string? DbEngine
    {
        get
        {
            if (Category != "database") return null;
            var firstLib = SupportedLibraries.FirstOrDefault();
            if (firstLib == null) return null;
            var lower = firstLib.ToLowerInvariant();
            if (lower.Contains("pg") || lower.Contains("npgsql") || lower.Contains("postgres"))
                return "PostgreSQL";
            if (lower.Contains("sqlclient") || lower.Contains("mssql") || lower.Contains("entityframeworkcore.sqlserver"))
                return "SQL Server";
            if (lower.Contains("sqlite"))
                return "SQLite";
            if (lower.Contains("mysql"))
                return "MySQL";
            if (lower.Contains("mongo"))
                return "MongoDB";
            if (lower.Contains("redis"))
                return "Redis";
            if (lower.Contains("clickhouse"))
                return "ClickHouse";
            return firstLib;
        }
    }

    /// <summary>
    /// For database category, the database type (e.g. "relational", "document", "keyvalue", "graph", "olap").
    /// </summary>
    string? DbType
    {
        get
        {
            if (Category != "database") return null;
            var firstLib = SupportedLibraries.FirstOrDefault();
            if (firstLib == null) return "unknown";
            var lower = firstLib.ToLowerInvariant();
            if (lower.Contains("redis"))
                return "keyvalue";
            if (lower.Contains("mongo") || lower.Contains("couch"))
                return "document";
            if (lower.Contains("neo4j") || lower.Contains("memgraph"))
                return "graph";
            if (lower.Contains("clickhouse") || lower.Contains("snowflake") || lower.Contains("duckdb"))
                return "olap";
            return "relational";
        }
    }

    /// <summary>
    /// For api category, the client library name (e.g. "Axios", "HttpClient").
    /// </summary>
    string? ApiLibrary
    {
        get
        {
            if (Category != "api") return null;
            var firstLib = SupportedLibraries.FirstOrDefault();
            if (firstLib == null) return null;
            var lower = firstLib.ToLowerInvariant();
            if (lower.Contains("axios"))
                return "Axios";
            if (lower == "net/http" || lower == "http" || lower == "https")
                return "http/https";
            if (lower.Contains("system.net.http") || lower == "httpclient")
                return "HttpClient";
            if (lower.Contains("fetch") || lower.Contains("isomorphic-fetch") || lower.Contains("cross-fetch"))
                return "fetch";
            if (lower.Contains("superagent"))
                return "superagent";
            if (lower.Contains("got"))
                return "got";
            if (lower.Contains("requests") || lower.Contains("urllib"))
                return "requests";
            if (lower.Contains("httpx"))
                return "httpx";
            if (lower.Contains("aiohttp"))
                return "aiohttp";
            if (lower.Contains("restsharp"))
                return "RestSharp";
            if (lower.Contains("flurl"))
                return "Flurl";
            if (lower.Contains("refit"))
                return "Refit";
            if (lower.Contains("resty"))
                return "Resty";
            if (lower.Contains("req"))
                return "req";
            if (lower.Contains("grequests"))
                return "grequests";
            if (lower.Contains("undici"))
                return "undici";
            return firstLib;
        }
    }

    /// <summary>
    /// For cloud category, the cloud service/provider name (e.g. "AWS", "GCP", "Azure", "Stripe").
    /// </summary>
    string? CloudService
    {
        get
        {
            if (Category != "cloud") return null;
            var firstLib = SupportedLibraries.FirstOrDefault();
            if (firstLib == null) return null;
            var lower = firstLib.ToLowerInvariant();
            if (lower.Contains("aws") || lower.Contains("boto3"))
                return "AWS";
            if (lower.Contains("google.cloud") || lower.Contains("google-cloud") || lower.Contains("firebase"))
                return "GCP";
            if (lower.Contains("azure"))
                return "Azure";
            if (lower.Contains("stripe"))
                return "Stripe";
            return firstLib;
        }
    }

    /// <summary>
    /// Maps a Tree-sitter AST node to a CodeExplorer ontological kind (Class, Interface, Function, Variable, Query, EntryPoint, ExternalService, or null).
    /// </summary>
    string? MapNodeType(Node node, ParsingContext ctx);

    /// <summary>
    /// Extracts the identifier/name of the matched behavior node.
    /// </summary>
    string? ExtractIdentifier(Node node, ParsingContext ctx);

    /// <summary>
    /// Collects references inside a scope for this library's nodes.
    /// </summary>
    void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx);
}
