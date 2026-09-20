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
    public async Task Benchmark_ResolveGlobalReferences_3M()
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

        // 1. Setup 50,000 RawTypeBindings
        for (int i = 0; i < 50_000; i++)
        {
            var file = $"src/File_{i % 2000}.cs";
            var varName = $"var_{i % 100}";
            var typeName = $"Type_{i % 500}";
            var scope = $"Method_{i % 50}";
            ctx.RawTypeBindings.Add(new RawTypeBinding(varName, typeName, file, scope));
        }

        // 2. Setup 150,000 GlobalSymbols
        for (int i = 0; i < 50_000; i++)
        {
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Type, $"Type_{i}", $"1:symbol:src/File_{i}.cs:Type:Type_{i}:1");
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, $"Type_{i % 500}.Method_{i % 50}", $"1:symbol:src/File_{i}.cs:Function:Method_{i % 50}:10");
            ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Table, $"Table_{i}", $"1:symbol:src/Db.sql:Table:Table_{i}:1");
        }

        // 3. Setup 3,100,000 GlobalReferences (identical scale and distribution to pow3)
        // - 15,000 Implements/InheritsFrom
        for (int i = 0; i < 15_000; i++)
        {
            var kind = i % 2 == 0 ? OntologyConstants.Relationships.Implements : OntologyConstants.Relationships.InheritsFrom;
            ctx.GlobalReferences.Add(new Reference($"1:symbol:src/File_{i % 2000}.cs:Type:Class_{i}:5", $"Type_{i % 50000}", kind));
        }

        // - 2,000,000 Calls with dot (half matching a binding, half unresolvable like static/BCL)
        for (int i = 0; i < 2_000_000; i++)
        {
            var file = $"src/File_{i % 2000}.cs";
            var scope = $"1:symbol:{file}:Function:Method_{i % 50}:20";
            string target;
            if (i % 2 == 0)
            {
                target = $"var_{i % 100}.Method_{i % 50}";
            }
            else
            {
                target = $"UnknownType_{i % 1000}.DoWork";
            }
            ctx.GlobalReferences.Add(new Reference(scope, target, OntologyConstants.Relationships.Calls));
        }

        // - 800,000 UsesType / PotentialType
        for (int i = 0; i < 800_000; i++)
        {
            var file = $"src/File_{i % 2000}.cs";
            var scope = $"1:symbol:{file}:Function:Method_{i % 50}:30";
            var kind = i % 2 == 0 ? OntologyConstants.Relationships.UsesType : OntologyConstants.Relationships.PotentialType;
            ctx.GlobalReferences.Add(new Reference(scope, $"Type_{i % 50000}", kind));
        }

        // - 285,000 other references
        for (int i = 0; i < 285_000; i++)
        {
            var file = $"src/File_{i % 2000}.cs";
            var scope = $"1:symbol:{file}:Function:Method_{i % 50}:40";
            ctx.GlobalReferences.Add(new Reference(scope, $"Table_{i % 50000}", OntologyConstants.Relationships.DependsOn));
        }

        Assert.That(ctx.GlobalReferences.Count, Is.EqualTo(3_100_000));

        var parser = new Layer5AnalysisParser();
        var method = typeof(Layer5AnalysisParser).GetMethod("ResolveAndUploadGlobalReferencesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var sw = Stopwatch.StartNew();
        var task = (Task<List<Relationship>>)method.Invoke(parser, [ctx])!;
        var results = await task;
        sw.Stop();

        Assert.That(results.Count, Is.GreaterThan(0));
    }
}