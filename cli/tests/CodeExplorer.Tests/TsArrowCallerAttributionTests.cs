using System.Threading.Channels;
using AwesomeAssertions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

/// <summary>
/// Arrow functions that are values of an object-literal property (`loader: () => http.get(...)`,
/// Angular rxResource/resource, NgRx effects, React Query options) or of a class field
/// (`handler = () => ...`) were unnamed => not a Function scope => the HTTP call was attributed
/// to the enclosing class Type. SIG-A-B-001 then reported every such call as "dangling".
/// </summary>
[TestFixture]
public class TsArrowCallerAttributionTests
{
    private static async Task<List<ExternalServiceNode>> ParseAsync(string code, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_arrow_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, fileName);
            await File.WriteAllTextAsync(filePath, code);
            var channel = Channel.CreateUnbounded<Func<Task>>();
            var ctx = new ParsingContext(tempDir, tempDir, new InMemoryGraphClient(), channel);
            using var syntaxTree = await new TypeScriptParser().ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            Layer3SyntacticParser.ProcessVisitor(syntaxTree, ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            var result = new List<ExternalServiceNode>();
            Collect(syntaxTree.FileNode.Children, result);
            return result;
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static void Collect(IEnumerable<IOntologyNode> nodes, List<ExternalServiceNode> acc)
    {
        foreach (var n in nodes)
        {
            if (n is ExternalServiceNode es) acc.Add(es);
            Collect(n.Children, acc);
        }
    }

    private static string Caller(ExternalServiceNode es) =>
        es.Extensions != null && es.Extensions.TryGetValue("caller_symbol_id", out var c) ? c : "";

    [Test]
    public async Task Angular_RxResource_LoaderArrow_IsAttributedToFunction()
    {
        var code = """
        import { Injectable, inject } from '@angular/core';
        import { HttpClient } from '@angular/common/http';
        import { rxResource } from '@angular/core/rxjs-interop';

        @Injectable({ providedIn: 'root' })
        export class TrackerApiService {
          private http = inject(HttpClient);

          public plansResource = rxResource({
            loader: () => this.http.get<any[]>(`https://bff.example.com/cpm-stats`),
          });

          public partnerTbsResource = rxResource({
            stream: () => this.http.get<any[]>(`https://bff.example.com/cpm-stats/click-types-partner`),
          });
        }
        """;

        var services = await ParseAsync(code, "tracker-api.service.ts");

        services.Should().HaveCount(2);
        foreach (var es in services)
        {
            Caller(es).Should().Contain(":Function:", $"call to {es.Path} must belong to a Function, got '{Caller(es)}'");
        }
        services.Select(Caller).Should().Contain(x => x.Contains("plansResource.loader"));
        services.Select(Caller).Should().Contain(x => x.Contains("partnerTbsResource.stream"));
    }

    [Test]
    public async Task ClassField_Arrow_IsNamedAfterField_NotAfterItsParameter()
    {
        var code = """
        import { HttpClient } from '@angular/common/http';

        export class Store {
          constructor(private http: HttpClient) {}

          load = (id: string) => this.http.get(`https://api.example.com/items/${id}`);
        }
        """;

        var services = await ParseAsync(code, "store.ts");

        services.Should().HaveCount(1);
        Caller(services[0]).Should().EndWith(":Function:load");
    }

    [Test]
    public async Task CallbackArgument_StaysAnonymous()
    {
        // Arrows passed as plain call arguments (map/filter/subscribe callbacks) must not become Functions.
        var code = """
        import { HttpClient } from '@angular/common/http';

        export class Svc {
          constructor(private http: HttpClient) {}

          loadAll(ids: string[]) {
            return ids.map(id => this.http.get(`https://api.example.com/items/${id}`));
          }
        }
        """;

        var services = await ParseAsync(code, "svc.ts");

        services.Should().HaveCount(1);
        Caller(services[0]).Should().EndWith(":Function:loadAll");
    }
}
