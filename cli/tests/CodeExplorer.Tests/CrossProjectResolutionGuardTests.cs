using CodeExplorer.Core.Database;
using System.Threading.Channels;
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
        var client = new InMemoryGraphRepository();
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
        var client = new InMemoryGraphRepository();
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
        var client = new InMemoryGraphRepository();
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

        var triggerRel = rels.FirstOrDefault(r => r.From == endpointId && r.Kind == RelationshipKind.Triggers);
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

    [Test]
    public async Task PrivateMethod_IsVisibleWithinSameClassAndPartialClass_ButBlockedFromOtherClass()
    {
        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphRepository();
        var ctx = new ParsingContext("c:/work", "c:/work", client, channel);
        ctx.WorkspaceId = "ws";

        var file1 = "OrderService/Services/OrderProcessor.Part1.cs";
        var file2 = "OrderService/Services/OrderProcessor.Part2.cs";

        var privateHelperId = $"ws:symbol:{file1}:Function:OrderProcessor.CalculateTax";
        var callerSameFileSameClassId = $"ws:symbol:{file1}:Function:OrderProcessor.Process";
        var callerPartialClassId = $"ws:symbol:{file2}:Function:OrderProcessor.ProcessBatch";
        var callerOtherClassSameFileId = $"ws:symbol:{file1}:Function:AuditLogger.Log";

        ctx.AddFileScopedSymbol(file1, OntologyConstants.NodeLabels.Function, "CalculateTax", privateHelperId);
        ctx.AddFileScopedSymbol(file1, OntologyConstants.NodeLabels.Function, "OrderProcessor.CalculateTax", privateHelperId);
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "CalculateTax", privateHelperId);
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "OrderProcessor.CalculateTax", privateHelperId);

        ctx.SymbolScopes[privateHelperId] = new SymbolScopeInfo(privateHelperId, file1, "OrderService", "OrderService.Services", "OrderProcessor", SymbolVisibility.Private);
        ctx.SymbolScopes[callerSameFileSameClassId] = new SymbolScopeInfo(callerSameFileSameClassId, file1, "OrderService", "OrderService.Services", "OrderProcessor", SymbolVisibility.Public);
        ctx.SymbolScopes[callerPartialClassId] = new SymbolScopeInfo(callerPartialClassId, file2, "OrderService", "OrderService.Services", "OrderProcessor", SymbolVisibility.Public);
        ctx.SymbolScopes[callerOtherClassSameFileId] = new SymbolScopeInfo(callerOtherClassSameFileId, file1, "OrderService", "OrderService.Services", "AuditLogger", SymbolVisibility.Public);

        ctx.AddGlobalReferences([
            new Reference(callerSameFileSameClassId, "CalculateTax", OntologyConstants.Relationships.Calls),
            new Reference(callerPartialClassId, "CalculateTax", OntologyConstants.Relationships.Calls),
            new Reference(callerOtherClassSameFileId, "CalculateTax", OntologyConstants.Relationships.Calls)
        ]);

        var layer5 = new Layer5AnalysisParser();
        var resolveMethod = typeof(Layer5AnalysisParser).GetMethod(
            "ResolveAndUploadGlobalReferencesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var rels = await (Task<List<Relationship>>)resolveMethod.Invoke(layer5, [ctx])!;

        rels.Should().Contain(r => r.From == callerSameFileSameClassId && r.To == privateHelperId);
        rels.Should().Contain(r => r.From == callerPartialClassId && r.To == privateHelperId);
        rels.Should().NotContain(r => r.From == callerOtherClassSameFileId && r.To == privateHelperId,
            "AuditLogger must not link to OrderProcessor's private CalculateTax method");
    }

    [Test]
    public async Task FilePrivateFunction_IsVisibleWithinSameFile_ButBlockedFromOtherFilesWithoutImport()
    {
        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphRepository();
        var ctx = new ParsingContext("c:/work", "c:/work", client, channel);
        ctx.WorkspaceId = "ws";

        var utilsFile = "web/src/utils/format.ts";
        var pageFile = "web/src/pages/home.ts";

        var filePrivateFnId = $"ws:symbol:{utilsFile}:Function:sanitizeInternal";
        var sameFileCallerId = $"ws:symbol:{utilsFile}:Function:formatTitle";
        var otherFileCallerId = $"ws:symbol:{pageFile}:Function:renderHome";

        ctx.AddFileScopedSymbol(utilsFile, OntologyConstants.NodeLabels.Function, "sanitizeInternal", filePrivateFnId);
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "sanitizeInternal", filePrivateFnId);

        ctx.SymbolScopes[filePrivateFnId] = new SymbolScopeInfo(filePrivateFnId, utilsFile, "web", null, null, SymbolVisibility.FilePrivate);
        ctx.SymbolScopes[sameFileCallerId] = new SymbolScopeInfo(sameFileCallerId, utilsFile, "web", null, null, SymbolVisibility.Public);
        ctx.SymbolScopes[otherFileCallerId] = new SymbolScopeInfo(otherFileCallerId, pageFile, "web", null, null, SymbolVisibility.Public);

        ctx.AddGlobalReferences([
            new Reference(sameFileCallerId, "sanitizeInternal", OntologyConstants.Relationships.Calls),
            new Reference(otherFileCallerId, "sanitizeInternal", OntologyConstants.Relationships.Calls)
        ]);

        var layer5 = new Layer5AnalysisParser();
        var resolveMethod = typeof(Layer5AnalysisParser).GetMethod(
            "ResolveAndUploadGlobalReferencesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var rels = await (Task<List<Relationship>>)resolveMethod.Invoke(layer5, [ctx])!;

        rels.Should().ContainSingle(r => r.From == sameFileCallerId && r.To == filePrivateFnId);
        rels.Should().NotContain(r => r.From == otherFileCallerId && r.To == filePrivateFnId);
    }

    [Test]
    public async Task CrossProjectCall_RespectsProjectDependencyReachability_AndDisambiguatesByImportedNamespace()
    {
        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphRepository();
        var ctx = new ParsingContext("c:/work", "c:/work", client, channel);
        ctx.WorkspaceId = "ws";

        var callerFile = "OrderApi/Controllers/OrderController.cs";
        var billingFile = "BillingContracts/BillingHelper.cs";
        var shippingFile = "ShippingContracts/ShippingHelper.cs";
        var isolatedAdminFile = "IsolatedAdmin/AdminSecret.cs";

        var callerId = $"ws:symbol:{callerFile}:Function:OrderController.Create";
        var billingValidateId = $"ws:symbol:{billingFile}:Function:ValidatePayload";
        var shippingValidateId = $"ws:symbol:{shippingFile}:Function:ValidatePayload";
        var isolatedAuditId = $"ws:symbol:{isolatedAdminFile}:Function:RunInternalAudit";

        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "ValidatePayload", billingValidateId);
        ctx.AddFileScopedSymbol(billingFile, OntologyConstants.NodeLabels.Function, "ValidatePayload", billingValidateId);
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "ValidatePayload", shippingValidateId);
        ctx.AddFileScopedSymbol(shippingFile, OntologyConstants.NodeLabels.Function, "ValidatePayload", shippingValidateId);
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "RunInternalAudit", isolatedAuditId);
        ctx.AddFileScopedSymbol(isolatedAdminFile, OntologyConstants.NodeLabels.Function, "RunInternalAudit", isolatedAuditId);

        ctx.SymbolScopes[callerId] = new SymbolScopeInfo(callerId, callerFile, "OrderApi", "OrderApi.Controllers", "OrderController", SymbolVisibility.Public);
        ctx.SymbolScopes[billingValidateId] = new SymbolScopeInfo(billingValidateId, billingFile, "BillingContracts", "Company.Billing", "BillingHelper", SymbolVisibility.Public);
        ctx.SymbolScopes[shippingValidateId] = new SymbolScopeInfo(shippingValidateId, shippingFile, "ShippingContracts", "Company.Shipping", "ShippingHelper", SymbolVisibility.Public);
        ctx.SymbolScopes[isolatedAuditId] = new SymbolScopeInfo(isolatedAuditId, isolatedAdminFile, "IsolatedAdmin", "Company.Admin", "AdminSecret", SymbolVisibility.Public);

        // OrderApi depends on BillingContracts and ShippingContracts, NOT on IsolatedAdmin
        ctx.GlobalProjectDependencies.Add(new Relationship("ws:proj:OrderApi", "ws:proj:BillingContracts", OntologyConstants.Relationships.DependsOn));
        ctx.GlobalProjectDependencies.Add(new Relationship("ws:proj:OrderApi", "ws:proj:ShippingContracts", OntologyConstants.Relationships.DependsOn));
        ctx.GlobalProjectDependencies.Add(new Relationship("ws:proj:IsolatedAdmin", "ws:proj:BillingContracts", OntologyConstants.Relationships.DependsOn));

        // OrderController.cs has `using Company.Shipping;`
        ctx.FileImportedNamespaces[callerFile] = new HashSet<string>(StringComparer.Ordinal) { "Company.Shipping" };

        ctx.AddGlobalReferences([
            new Reference(callerId, "ValidatePayload", OntologyConstants.Relationships.Calls),
            new Reference(callerId, "RunInternalAudit", OntologyConstants.Relationships.Calls)
        ]);

        var layer5 = new Layer5AnalysisParser();
        var resolveMethod = typeof(Layer5AnalysisParser).GetMethod(
            "ResolveAndUploadGlobalReferencesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var rels = await (Task<List<Relationship>>)resolveMethod.Invoke(layer5, [ctx])!;

        rels.Should().ContainSingle(r => r.From == callerId && r.To == shippingValidateId,
            "should disambiguate ValidatePayload via imported namespace Company.Shipping");
        rels.Should().NotContain(r => r.From == callerId && r.To == isolatedAuditId,
            "OrderApi does not depend on IsolatedAdmin, so RunInternalAudit is unreachable");
    }

    [Test]
    public async Task GlobalBindingsLookup_DoesNotLeakLocalVariableTypeAcrossUnrelatedFiles()
    {
        var channel = Channel.CreateUnbounded<Func<Task>>();
        var client = new InMemoryGraphRepository();
        var ctx = new ParsingContext("c:/work", "c:/work", client, channel);
        ctx.WorkspaceId = "ws";

        var fileA = "ServiceA/WorkerA.cs";
        var fileB = "ServiceA/WorkerB.cs";

        var specialClientSendId = $"ws:symbol:{fileA}:Function:SpecialClient.Send";
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Type, "SpecialClient", $"ws:symbol:{fileA}:Type:SpecialClient");
        ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Function, "SpecialClient.Send", specialClientSendId);
        ctx.AddFileScopedSymbol(fileA, OntologyConstants.NodeLabels.Function, "SpecialClient.Send", specialClientSendId);

        // Local variable `client` is bound to `SpecialClient` ONLY in fileA (inside MethodA)
        ctx.RawTypeBindings.Add(new RawTypeBinding("client", "SpecialClient", fileA, "MethodA"));

        // Caller in fileB (MethodB) calls `client.Send()` where `client` has no binding in fileB
        var callerInFileB = $"ws:symbol:{fileB}:Function:WorkerB.MethodB";
        ctx.AddGlobalReferences([
            new Reference(callerInFileB, "client.Send", OntologyConstants.Relationships.Calls)
        ]);

        var layer5 = new Layer5AnalysisParser();
        var resolveMethod = typeof(Layer5AnalysisParser).GetMethod(
            "ResolveAndUploadGlobalReferencesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var rels = await (Task<List<Relationship>>)resolveMethod.Invoke(layer5, [ctx])!;

        rels.Should().BeEmpty("local variable binding in fileA must not leak into unrelated fileB");
    }
}

