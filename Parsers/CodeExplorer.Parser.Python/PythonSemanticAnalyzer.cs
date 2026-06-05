using System;
using System.Collections.Generic;
using System.IO;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Python;

public class PythonSemanticAnalyzer : BaseSemanticAnalyzer
{
    private static readonly Dictionary<string, HashSet<string>> DbPackagesByKind = new(StringComparer.OrdinalIgnoreCase)
    {
        ["relational"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "sqlalchemy", "peewee", "pony", "sqlmodel", "tortoise", "dataset", "django.db", "django.db.models",
            "sqlite3", "sqlite-utils", "psycopg", "psycopg2", "mysqlclient", "pymysql", "mysql.connector",
            "cassandra-driver", "duckdb", "chdb"
        },
        ["document"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "beanie", "mongoengine", "pymongo", "tinydb", "pickledb", "zodb"
        },
        ["keyvalue"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "redis", "redis-py", "aioredis"
        },
        ["vector"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "chromadb"
        },
        ["search_analytic"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "clickhouse-driver"
        }
    };

    protected override IReadOnlyDictionary<string, HashSet<string>> DbPackages => DbPackagesByKind;

    private static readonly HashSet<string> ApiPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "requests", "urllib.request", "urllib3", "httpx", "aiohttp"
    };

    private static readonly HashSet<string> CloudPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "boto3", "stripe", "firebase-admin"
    };

    protected override bool IsApiPackage(string importPath)
    {
        return ApiPackages.Contains(importPath) || ApiPackages.Contains(Path.GetFileName(importPath));
    }

    protected override bool IsCloudPackage(string importPath)
    {
        return CloudPackages.Contains(importPath) ||
               CloudPackages.Contains(Path.GetFileName(importPath)) ||
               importPath.StartsWith("google-cloud-", StringComparison.OrdinalIgnoreCase) ||
               importPath.StartsWith("google.cloud", StringComparison.OrdinalIgnoreCase) ||
               importPath.StartsWith("azure-", StringComparison.OrdinalIgnoreCase) ||
               importPath.StartsWith("azure.", StringComparison.OrdinalIgnoreCase);
    }
}
