using System.Diagnostics;
using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class Layer5BenchmarkTests
{
    [Test]
    public async Task Benchmark_ResolveGlobalReferences_100K()
    {
        var dbClient = new InMemoryGraphClient();
        var channel = Channel.CreateUnbounded<Func<Task>>();
        var ctx = new ParsingContext(
            absoluteWorkspacePath: "C:/mock/workspace",
            hostWorkspacePath: "C:/mock/workspace",
            dbClient: dbClient,
            sharedChannel: channel,
            clear: false,
            logger: NullLogger.Instance
        );
        ctx.WorkspaceId = "1";

        // 1. Setup 2,000 RawTypeBindings
        for (int i = 0; i < 2_000; i++)
        {
            var file = $"src/File_{i % 200}.cs";
            var varName = $"var_{i % 50}";
            var typeName = $"Type_{i % 200}";
            var scope = $"Method_{i % 20}";
            ctx.RawTypeBindings.Add(new RawTypeBinding(varName, typeName, file, scope));
        }

        // 2. Setup 6,000 GlobalSymbols
        for (int i = 0; i < 2_000; i++)
        {
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Type, $"Type_{i}", $"1:symbol:src/File_{i}.cs:Type:Type_{i}:1");
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, $"Type_{i % 200}.Method_{i % 20}", $"1:symbol:src/File_{i}.cs:Function:Method_{i % 20}:10");
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, $"Table_{i}", $"1:symbol:src/Db.sql:Table:Table_{i}:1");
        }

        // 3. Setup 100,000 GlobalReferences (identical distribution to pow3 at 100k scale)
        // - 500 Implements/InheritsFrom
        for (int i = 0; i < 500; i++)
        {
            var kind = i % 2 == 0 ? OntologyConstants.Relationships.Implements : OntologyConstants.Relationships.InheritsFrom;
            ctx.GlobalReferences.Add(new Reference($"1:symbol:src/File_{i % 200}.cs:Type:Class_{i}:5", $"Type_{i % 2000}", kind));
        }

        // - 65,000 Calls with dot (half matching a binding, half unresolvable like static/BCL)
        for (int i = 0; i < 65_000; i++)
        {
            var file = $"src/File_{i % 200}.cs";
            var scope = $"1:symbol:{file}:Function:Method_{i % 20}:20";
            string target;
            if (i % 2 == 0)
            {
                target = $"var_{i % 50}.Method_{i % 20}";
            }
            else
            {
                target = $"UnknownType_{i % 500}.DoWork";
            }
            ctx.GlobalReferences.Add(new Reference(scope, target, OntologyConstants.Relationships.Calls));
        }

        // - 25,000 UsesType / PotentialType
        for (int i = 0; i < 25_000; i++)
        {
            var file = $"src/File_{i % 200}.cs";
            var scope = $"1:symbol:{file}:Function:Method_{i % 20}:30";
            var kind = i % 2 == 0 ? OntologyConstants.Relationships.UsesType : OntologyConstants.Relationships.PotentialType;
            ctx.GlobalReferences.Add(new Reference(scope, $"Type_{i % 2000}", kind));
        }

        // - 9,500 other references
        for (int i = 0; i < 9_500; i++)
        {
            var file = $"src/File_{i % 200}.cs";
            var scope = $"1:symbol:{file}:Function:Method_{i % 20}:40";
            ctx.GlobalReferences.Add(new Reference(scope, $"Table_{i % 2000}", OntologyConstants.Relationships.DependsOn));
        }

        ctx.GlobalReferences.Count.Should().Be(100_000);

        var parser = new Layer5AnalysisParser();
        var method = typeof(Layer5AnalysisParser).GetMethod("ResolveAndUploadGlobalReferencesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var sw = Stopwatch.StartNew();
        var task = (Task<List<Relationship>>)method.Invoke(parser, [ctx])!;
        var results = await task;
        sw.Stop();

        results.Count.Should().BeGreaterThan(0);
    }
}