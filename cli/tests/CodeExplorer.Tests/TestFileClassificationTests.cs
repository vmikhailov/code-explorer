using NUnit.Framework;

namespace CodeExplorer.Tests;

/// <summary>
/// A file is a test when the test-framework component parser classifies it so — not by "test" in the path.
/// Angular/Nest `*.spec.ts` under `src/` used to stay is_test=false (component parsers got an empty
/// dependency list, so the Jest/Jasmine runner never matched), and every describe/it title became a
/// CallLiteral counted by SIG-M-I-001.
/// </summary>
[TestFixture]
public class TestFileClassificationTests
{
    private const string SpecCode = """
        describe('TargetUrlGeneratorHelper', () => {
          it('builds the link', () => {
            expect(helper.generateLink('campaign-42')).toBeDefined();
          });
        });
        """;

    private const string ProdCode = """
        export class TargetUrlGeneratorHelper {
          generateLink(id: string) { return this.urls.get('campaign-link'); }
        }
        """;

    private static async Task<string> IndexAndQueryAsync(string packageJson, string query)
    {
        var ws = Path.Combine(Path.GetTempPath(), "ce_cls_" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(ws, "svc", "src", "helpers");
        Directory.CreateDirectory(src);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(ws, "svc", "package.json"), packageJson);
            await File.WriteAllTextAsync(Path.Combine(src, "target-url-generator.helper.ts"), ProdCode);
            await File.WriteAllTextAsync(Path.Combine(src, "target-url-generator.helper.spec.ts"), SpecCode);

            await using var client = new SqliteGraphClient(Path.Combine(ws, "graph.db"));
            WorkspaceIndexer.AutoRegisterParsers();
            await new WorkspaceIndexer(client).IndexAsync(ws, ws, clear: true);
            return await client.ExecuteQueryAsync(query);
        }
        finally
        {
            try { Directory.Delete(ws, true); } catch { }
        }
    }

    private const string JestPkg = """{ "name": "svc", "dependencies": { "@nestjs/core": "^10.0.0", "@nestjs/common": "^10.0.0" }, "devDependencies": { "jest": "^29.7.0" } }""";
    private const string KarmaPkg = """{ "name": "svc", "dependencies": { "@angular/core": "^20.0.0", "@angular/common": "^20.0.0" }, "devDependencies": { "jasmine-core": "~4.5.0", "karma": "~6.4.0" } }""";

    [TestCase(JestPkg)]
    [TestCase(KarmaPkg)]
    public async Task SpecFile_UnderSrc_IsClassifiedAsTest_AndItsLiteralsCarryIsTest(string pkg)
    {
        var files = await IndexAndQueryAsync(pkg,
            "MATCH (f:File) WHERE f.name ENDS WITH '.spec.ts' RETURN f.is_test AS t");
        (files.Contains("\"t\": true") || files.Contains("\"t\": 1")).Should().BeTrue();

        var lits = await IndexAndQueryAsync(pkg,
            "MATCH (l:CallLiteral) WHERE l.file_path ENDS WITH '.spec.ts' AND NOT coalesce(l.is_test, false) RETURN count(l) AS c");
        lits.Should().Contain("\"c\": 0", lits);
    }

    [Test]
    public async Task ProductionFile_IsNotTest()
    {
        var res = await IndexAndQueryAsync(JestPkg,
            "MATCH (f:File) WHERE f.name = 'target-url-generator.helper.ts' RETURN f.is_test AS t");
        (res.Contains("\"t\": false") || res.Contains("\"t\": 0")).Should().BeTrue();
    }
}
