using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Layers;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[NonParallelizable]
public class CrossProjectResolutionGuardTests
{
    private IDisposable? _lock;

    [SetUp]
    public async Task SetUp()
    {
        _lock = await TestRegistryLock.AcquireAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _lock?.Dispose();
        _lock = null;
    }

    [Test]
    public async Task UnresolvedReceiver_MethodCall_DoesNotBindToForeignProjectFunction()
    {
        // fe/src/app.service.ts calls this.http.get('/bundles')
        // TargetName is "this.http.get".
        // bundle-update-adapter defines function "get".
        // fe has no dependency on bundle-update-adapter.
        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphClient();
        var ctx = new ParsingContext("c:/work", "c:/work", client, channel);
        ctx.WorkspaceId = "ws";

        // Register function 'get' in bundle-update-adapter
        var adapterFuncId = "ws:symbol:services/bundle-update-adapter/src/shared/api/api.service.ts:Function:get";
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "get", adapterFuncId);

        // Caller in fe
        var callerSymbolId = "ws:symbol:services/fe/src/app/common-api.service.ts:Function:stream";

        // Reference emitted by parser for this.http.get(...)
        ctx.AddGlobalReferences([
            new Reference(callerSymbolId, "this.http.get", OntologyConstants.Relationships.Calls)
        ]);

        var layer5 = new Layer5AnalysisParser();
        var resolveMethod = typeof(Layer5AnalysisParser).GetMethod(
            "ResolveAndUploadGlobalReferencesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var task = (Task<List<Relationship>>)resolveMethod.Invoke(layer5, [ctx])!;
        var rels = await task;

        var spuriousCall = rels.FirstOrDefault(r => r.From == callerSymbolId && r.To == adapterFuncId);
        spuriousCall.Should().BeNull("an unresolved receiver (this.http.get) must never bind to a foreign project's function");
    }

    [Test]
    public async Task ExternalTypeBoundReceiver_MethodCall_DoesNotFallbackToBareMethodName()
    {
        // fe has private http = inject(HttpClient) -> RawTypeBinding("http", "HttpClient", "fe/src/app/common-api.service.ts", "CommonApiService")
        // HttpClient is an external Angular class (not in TypeSymbols).
        // Calling this.http.get('/bundles') must NOT strip 'HttpClient.' and fallback to bare 'get' in fe or bundle-update-adapter.
        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphClient();
        var ctx = new ParsingContext("c:/work", "c:/work", client, channel);
        ctx.WorkspaceId = "ws";

        var feFile = "fe/src/app/common-api.service.ts";
        var adapterFile = "bundle-update-adapter/src/shared/api/api.service.ts";

        var adapterGetId = $"ws:symbol:{adapterFile}:Function:get";
        var feHelperGetId = $"ws:symbol:fe/src/app/shared/services/storage.service.ts:Function:get";
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "get", adapterGetId);
        ctx.AddFileScopedSymbol(adapterFile, OntologyConstants.NodeLabels.Function, "get", adapterGetId);
        ctx.AddFileScopedSymbol("fe/src/app/shared/services/storage.service.ts", OntologyConstants.NodeLabels.Function, "get", feHelperGetId);

        ctx.RawTypeBindings.Add(new RawTypeBinding("http", "HttpClient", feFile, "CommonApiService"));

        var callerSymbolId = $"ws:symbol:{feFile}:Function:stream";
        ctx.AddGlobalReferences([
            new Reference(callerSymbolId, "this.http.get", OntologyConstants.Relationships.Calls)
        ]);

        var layer5 = new Layer5AnalysisParser();
        var resolveMethod = typeof(Layer5AnalysisParser).GetMethod(
            "ResolveAndUploadGlobalReferencesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var task = (Task<List<Relationship>>)resolveMethod.Invoke(layer5, [ctx])!;
        var rels = await task;

        rels.Where(r => r.From == callerSymbolId).Should().BeEmpty(
            "a receiver bound to an external type (HttpClient) must not strip the receiver and bind to unrelated 'get' functions");
    }

    [Test]
    public void ExtractProjectName_HandlesWorkspaceRelativePathsWithNestedServicesAndModulesFolders()
    {
        ConstantRegistry.ExtractProjectName("fe/src/app/shared/services/api.service.ts").Should().Be("fe");
        ConstantRegistry.ExtractProjectName("bundle-update-adapter/src/shared/services/api.service.ts").Should().Be("bundle-update-adapter");
        ConstantRegistry.ExtractProjectName("bff/src/modules/bundles/bundles.controller.ts").Should().Be("bff");
        ConstantRegistry.ExtractProjectName("src/services/fe/src/app/shared/services/auth/auth.service.ts").Should().Be("fe");
        ConstantRegistry.ExtractProjectName("../libs/library/src/index.ts").Should().Be("library");
    }

    [Test]
    public async Task TriggersReference_BindsToSameProjectMethod_NotForeignProjectWithSameName()
    {
        // bff has an endpoint GET /bundles that triggers getBundles
        // bundle-update-adapter ALSO has a function named getBundles
        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphClient();
        var ctx = new ParsingContext("c:/work", "c:/work", client, channel);
        ctx.WorkspaceId = "ws";

        var bffFile = "services/bff/src/modules/bundles/bundles.controller.ts";
        var adapterFile = "services/bundle-update-adapter/src/modules/bundles-cache/bundles-cache.service.ts";

        var bffFuncId = $"ws:symbol:{bffFile}:Function:getBundles";
        var adapterFuncId = $"ws:symbol:{adapterFile}:Function:getBundles";

        // Register both functions
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "getBundles", adapterFuncId);
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "getBundles", bffFuncId);

        var endpointId = "ws:endpoint:bff:GET:/bundles";
        ctx.AddGlobalReferences([
            new Reference(endpointId, "getBundles", OntologyConstants.Relationships.Triggers)
        ]);

        var layer5 = new Layer5AnalysisParser();
        var resolveMethod = typeof(Layer5AnalysisParser).GetMethod(
            "ResolveAndUploadGlobalReferencesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var task = (Task<List<Relationship>>)resolveMethod.Invoke(layer5, [ctx])!;
        var rels = await task;

        var triggerRel = rels.FirstOrDefault(r => r.From == endpointId && r.Kind == OntologyConstants.Relationships.Triggers);
        triggerRel.Should().NotBeNull();
        triggerRel!.To.Should().Be(bffFuncId, "endpoint in bff must trigger bff's getBundles, not bundle-update-adapter's");
    }

    [Test]
    public void CategorizeBoundedContext_PreservesExactServiceName_DoesNotConvertToTitleCase()
    {
        var category = ArchitectureViewEngine.CategorizeBoundedContext(null, null, "bundle-scheduler");
        category.DisplayName.Should().Be("bundle-scheduler");
        category.DomainName.Should().Be("bundle-scheduler");
    }
}

