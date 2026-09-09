using System;
using System.IO;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Parser;
using CodeExplorer.Cypher.Tests.Shared;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace CodeExplorer.Cypher.Tests;

[TestFixture]
public class FileBasedCypherTests
{
    private SqliteConnection _conn = null!;

    [SetUp]
    public void SetUp()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE nodes (
                id TEXT PRIMARY KEY,
                kind TEXT NOT NULL,
                properties TEXT NOT NULL
            );

            CREATE TABLE edges (
                from_id TEXT NOT NULL,
                to_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                properties TEXT NOT NULL
            );

            CREATE INDEX idx_edges_from ON edges(from_id);
            CREATE INDEX idx_edges_to ON edges(to_id);
            CREATE INDEX idx_edges_kind ON edges(kind);
            CREATE INDEX idx_nodes_kind ON nodes(kind);
        ";
        cmd.ExecuteNonQuery();
    }

    [TearDown]
    public void TearDown()
    {
        _conn.Dispose();
    }

    [CypherFileSource("TestData/Queries")]
    public void Should_Parse_And_Compile_Cypher_Query(string filePath)
    {
        Assert.That(File.Exists(filePath), Is.True, $"File does not exist: {filePath}");

        var queryText = File.ReadAllText(filePath);

        // 1. Parse into AST
        var ast = CypherQueryParser.Parse(queryText);
        Assert.That(ast, Is.Not.Null, "AST must not be null");
        Assert.That(ast.Matches, Is.Not.Empty, "Query must have at least one MATCH clause");
        Assert.That(ast.Return, Is.Not.Null, "Query must have a RETURN clause");

        // 2. Compile to SQLite SQL
        var compiled = SqliteCompiler.Compile(ast);
        Assert.That(compiled.Sql, Is.Not.Null.And.Not.Empty, "Compiled SQL must not be empty");

        // 3. Verify SQLite validity via EXPLAIN QUERY PLAN
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = $"EXPLAIN QUERY PLAN {compiled.Sql}";
        foreach (var (k, v) in compiled.Parameters)
        {
            var paramName = "@" + k.TrimStart('@');
            cmd.Parameters.AddWithValue(paramName, v ?? "dummy_val");
        }

        // Add dummy values for any query-level parameters like $symbolParam
        var matches = System.Text.RegularExpressions.Regex.Matches(compiled.Sql, @"@[a-zA-Z0-9_]+");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var pName = match.Value;
            if (!cmd.Parameters.Contains(pName))
            {
                cmd.Parameters.AddWithValue(pName, "dummy_val");
            }
        }

        Assert.DoesNotThrow(() =>
        {
            using var reader = cmd.ExecuteReader();
        }, "SQLite failed to explain/validate compiled SQL");
    }
}

