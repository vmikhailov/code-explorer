using CodeExplorer.Core.Database;
using CodeExplorer.Parsers.TypeScript;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public sealed class TypeScriptDestructuredFunctionTypeTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ce_tsx_diag_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup on Windows
            }
        }
    }

    [Test]
    public void ParseDocument_TsxInterfaceWithUntypedDestructuredFunctionType_ProducesZeroDiagnostics()
    {
        const string tsxSource = """
            import React, { useEffect, useState } from 'react';

            interface DatatableProps {
              keyAccessor?: string;
              hasToolbar?: boolean;
              renderTopToolbar?: React.ReactNode | (({ table }) => React.ReactNode);
              columns?: any;
            }

            export const DataTable = (props: DatatableProps) => {
              const [data, setData] = useState([]);
              useEffect(() => {
                setData([]);
              }, []);
              return <div>{data.length}</div>;
            };
            """;

        var parser = new TypeScriptFileParser();
        var doc = parser.ParseDocument(tsxSource, "DataTable.tsx", "src/DataTable.tsx");

        Assert.That(doc, Is.Not.Null);
        Assert.That(doc!.Diagnostics, Is.Empty);
        Assert.That(doc.AllTypes.Select(t => t.Name), Does.Contain("DatatableProps"));
        Assert.That(doc.AllMembers.Select(m => m.Name), Does.Contain("renderTopToolbar"));
    }

    [Test]
    public async Task IndexWorkspace_GenuineSyntaxErrorInTsx_RecordsSingleScanIssueWithoutDuplication()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "package.json"), """
            {
              "name": "broken-tsx-app",
              "version": "1.0.0"
            }
            """);

        var srcDir = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "Broken.tsx"), """
            export function BrokenComponent( {
              return <div>unclosed params;
            }
            """);

        var dbDir = Path.Combine(_tempDir, ".codeexplorer");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "graph.db");
        await using var client = new SqliteGraphClient(dbPath);
        await client.CreateIndicesAsync();

        var indexer = new WorkspaceIndexer(client, NullLogger<WorkspaceIndexer>.Instance);
        await indexer.IndexAsync(_tempDir, clear: true);

        var res = await client.ExecuteCypherAsync("MATCH (i:ScanIssue) RETURN i.id, i.path, i.message");
        Assert.That(res.Rows, Has.Count.EqualTo(1));
    }
}
