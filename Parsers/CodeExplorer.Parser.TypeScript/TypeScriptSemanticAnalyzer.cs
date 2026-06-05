using System;
using System.Collections.Generic;
using System.IO;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptSemanticAnalyzer : BaseSemanticAnalyzer
{
    private static readonly Dictionary<string, HashSet<string>> DbPackagesByKind = new(StringComparer.OrdinalIgnoreCase)
    {
        ["relational"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "sequelize", "typeorm", "prisma", "@prisma/client", "drizzle-orm", "knex", "objection", "bookshelf", 
            "mikro-orm", "@mikro-orm/core", "waterline", "slonik", "pg", "pg-promise", "mysql2", "mysql", 
            "sqlite3", "better-sqlite3", "cassandra-driver"
        },
        ["document"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "mongoose", "typegoose", "@typegoose/typegoose", "mongodb", "couchdb", "nano"
        },
        ["keyvalue"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "redis", "ioredis"
        },
        ["graph"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "neo4j-driver"
        },
        ["search_analytic"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "clickhouse", "@clickhouse/client", "@opensearch-project/opensearch", "@elastic/elasticsearch"
        },
        ["timeseries"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "influx", "@influxdata/influxdb-client"
        }
    };

    protected override IReadOnlyDictionary<string, HashSet<string>> DbPackages => DbPackagesByKind;

    private static readonly HashSet<string> ApiPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "axios", "request", "superagent", "node-fetch", "got", "@nestjs/axios", "undici", "ky", "bent", "urllib", "cross-fetch", "isomorphic-fetch"
    };

    private static readonly HashSet<string> CloudPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "aws-sdk", "stripe", "firebase", "firebase-admin"
    };

    protected override bool IsApiPackage(string importPath)
    {
        return ApiPackages.Contains(importPath) || ApiPackages.Contains(Path.GetFileName(importPath));
    }

    protected override bool IsCloudPackage(string importPath)
    {
        return CloudPackages.Contains(importPath) ||
               CloudPackages.Contains(Path.GetFileName(importPath)) ||
               importPath.StartsWith("@google-cloud/", StringComparison.OrdinalIgnoreCase) ||
               importPath.StartsWith("@azure/", StringComparison.OrdinalIgnoreCase) ||
               importPath.StartsWith("@aws-sdk/", StringComparison.OrdinalIgnoreCase);
    }
}
