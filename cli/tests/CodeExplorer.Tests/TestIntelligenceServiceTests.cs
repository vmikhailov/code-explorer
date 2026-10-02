using CodeExplorer.Core.Analysis.Testing;
using CodeExplorer.Core.Database;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class TestIntelligenceServiceTests
{
    private string _tempDir = null!;
    private SqliteGraphClient _db = null!;
    private TestIntelligenceService _service = null!;

    [SetUp]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ce_test_intel_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        var dbPath = Path.Combine(_tempDir, "graph.db").Replace('\\', '/');
        _db = new SqliteGraphClient(dbPath);
        _service = new TestIntelligenceService(_db);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Test]
    public void GitDiffHelper_ParseDiffHunks_ExtractsLineRangesCorrectly()
    {
        var sampleDiff = """
            diff --git a/src/Core/UserService.cs b/src/Core/UserService.cs
            index 1234567..89abcdef 100644
            --- a/src/Core/UserService.cs
            +++ b/src/Core/UserService.cs
            @@ -45,6 +45,8 @@ public void CreateUser()
             {
            +    ValidateUser();
            +    CheckPermissions();
                 Save();
             }
            @@ -120,4 +122,5 @@ public void DeleteUser()
             {
            +    LogAudit();
                 Remove();
             }
            """;

        var hunks = GitDiffHelper.ParseDiffHunks(sampleDiff);

        Assert.That(hunks, Has.Count.EqualTo(2));
        Assert.That(hunks[0].File, Is.EqualTo("src/Core/UserService.cs"));
        Assert.That(hunks[0].StartLine, Is.EqualTo(45));
        Assert.That(hunks[0].EndLine, Is.EqualTo(52));

        Assert.That(hunks[1].File, Is.EqualTo("src/Core/UserService.cs"));
        Assert.That(hunks[1].StartLine, Is.EqualTo(122));
        Assert.That(hunks[1].EndLine, Is.EqualTo(126));
    }

    [Test]
    public async Task AnalyzeCoverageAsync_ComputesCoveredAndUncoveredClassesAndMethods()
    {
        var projCore = new Node("proj:core", "Project", new Dictionary<string, object>
        {
            ["name"] = "CoreLib",
            ["path"] = "src/CoreLib",
            ["role"] = "Service",
            ["is_library"] = "true"
        });
        var projTests = new Node("proj:tests", "Project", new Dictionary<string, object>
        {
            ["name"] = "CoreTests",
            ["path"] = "tests/CoreTests",
            ["role"] = "Test",
            ["is_library"] = "false"
        });

        var typeUser = new Node("type:user_svc", "Type", new Dictionary<string, object>
        {
            ["name"] = "UserService",
            ["kind"] = "Class",
            ["file_path"] = "src/CoreLib/UserService.cs",
            ["project"] = "CoreLib",
            ["start_line"] = 10,
            ["end_line"] = 100
        });
        var typeOrder = new Node("type:order_svc", "Type", new Dictionary<string, object>
        {
            ["name"] = "OrderService",
            ["kind"] = "Class",
            ["file_path"] = "src/CoreLib/OrderService.cs",
            ["project"] = "CoreLib",
            ["start_line"] = 10,
            ["end_line"] = 80
        });

        var fnUserCreate = new Node("fn:user_create", "Function", new Dictionary<string, object>
        {
            ["name"] = "CreateUser",
            ["file_path"] = "src/CoreLib/UserService.cs",
            ["project"] = "CoreLib",
            ["start_line"] = 15,
            ["end_line"] = 30
        });
        var fnUserDelete = new Node("fn:user_delete", "Function", new Dictionary<string, object>
        {
            ["name"] = "DeleteUser",
            ["file_path"] = "src/CoreLib/UserService.cs",
            ["project"] = "CoreLib",
            ["start_line"] = 40,
            ["end_line"] = 50
        });
        var fnOrderPlace = new Node("fn:order_place", "Function", new Dictionary<string, object>
        {
            ["name"] = "PlaceOrder",
            ["file_path"] = "src/CoreLib/OrderService.cs",
            ["project"] = "CoreLib",
            ["start_line"] = 15,
            ["end_line"] = 40
        });

        var fnTestUserCreate = new Node("fn:test_user_create", "Function", new Dictionary<string, object>
        {
            ["name"] = "Test_CreateUser_Success",
            ["file_path"] = "tests/CoreTests/UserServiceTests.cs",
            ["project"] = "CoreTests",
            ["is_test"] = "true",
            ["test_framework"] = "nunit",
            ["start_line"] = 20,
            ["end_line"] = 35
        });

        await _db.UploadNodesAsync([
            projCore, projTests, typeUser, typeOrder, fnUserCreate, fnUserDelete, fnOrderPlace, fnTestUserCreate
        ]);

        var emptyProps = new Dictionary<string, object>();
        var edges = new List<Relationship>
        {
            new("type:user_svc", "fn:user_create", "HAS_METHOD", emptyProps),
            new("type:user_svc", "fn:user_delete", "HAS_METHOD", emptyProps),
            new("type:order_svc", "fn:order_place", "HAS_METHOD", emptyProps),
            new("fn:test_user_create", "fn:user_create", "CALLS", emptyProps)
        };
        await _db.UploadRelationshipsAsync(edges);

        var report = await _service.AnalyzeCoverageAsync(new TestCoverageFilter());

        Assert.That(report, Is.Not.Null);
        Assert.That(report.Summary.TotalClasses, Is.EqualTo(2));
        Assert.That(report.Summary.CoveredClasses, Is.EqualTo(1));
        Assert.That(report.Summary.UncoveredClasses, Is.EqualTo(1));
        Assert.That(report.Summary.ClassCoveragePercentage, Is.EqualTo(50.0));

        Assert.That(report.Summary.TotalMethods, Is.EqualTo(3));
        Assert.That(report.Summary.CoveredMethods, Is.EqualTo(1));
        Assert.That(report.Summary.UncoveredMethods, Is.EqualTo(2));
        Assert.That(Math.Round(report.Summary.MethodCoveragePercentage, 1), Is.EqualTo(33.3));

        Assert.That(report.CoveredClasses, Has.Count.EqualTo(1));
        Assert.That(report.CoveredClasses[0].Name, Is.EqualTo("UserService"));
        Assert.That(report.CoveredClasses[0].CoveredMethodsCount, Is.EqualTo(1));

        Assert.That(report.UncoveredClasses, Has.Count.EqualTo(1));
        Assert.That(report.UncoveredClasses[0].Name, Is.EqualTo("OrderService"));
        Assert.That(report.UncoveredClasses[0].MethodCount, Is.EqualTo(1));
    }

    [Test]
    public async Task AnalyzeImpactAsync_TracesTransitiveReverseCalls_ToExactTestMethods()
    {
        var fnRepo = new Node("fn:repo_save", "Function", new Dictionary<string, object>
        {
            ["name"] = "SaveEntity",
            ["file_path"] = "src/Repo/DataRepo.cs",
            ["project"] = "DataRepo",
            ["start_line"] = 10,
            ["end_line"] = 25
        });

        var fnService = new Node("fn:svc_process", "Function", new Dictionary<string, object>
        {
            ["name"] = "ProcessOrder",
            ["file_path"] = "src/Core/OrderService.cs",
            ["project"] = "OrderService",
            ["start_line"] = 30,
            ["end_line"] = 50
        });

        var fnTest1 = new Node("fn:test_order_process", "Function", new Dictionary<string, object>
        {
            ["name"] = "Test_ProcessOrder_EmitsEvent",
            ["file_path"] = "tests/OrderServiceTests.cs",
            ["project"] = "OrderTests",
            ["is_test"] = "true",
            ["test_framework"] = "nunit",
            ["start_line"] = 15,
            ["end_line"] = 35
        });

        await _db.UploadNodesAsync([fnRepo, fnService, fnTest1]);

        var emptyProps = new Dictionary<string, object>();
        await _db.UploadRelationshipsAsync([
            new("fn:svc_process", "fn:repo_save", "CALLS", emptyProps),
            new("fn:test_order_process", "fn:svc_process", "CALLS", emptyProps)
        ]);

        // 1. When RepoMethod changes, TIA should find Test_ProcessOrder_EmitsEvent with Depth = 2
        var reportRepo = await _service.AnalyzeImpactAsync(new TestImpactRequest
        {
            SymbolNames = new List<string> { "SaveEntity" }
        });

        Assert.That(reportRepo.AffectedTestMethods, Has.Count.EqualTo(1));
        var test = reportRepo.AffectedTestMethods[0];
        Assert.That(test.TestMethodName, Is.EqualTo("Test_ProcessOrder_EmitsEvent"));
        Assert.That(test.Depth, Is.EqualTo(2));
        Assert.That(test.TargetSymbol, Is.EqualTo("SaveEntity"));
        Assert.That(test.TestFramework, Is.EqualTo("nunit"));
        Assert.That(reportRepo.RunnerCommands, Contains.Key("dotnet"));
        Assert.That(reportRepo.RunnerCommands["dotnet"], Does.Contain("OrderServiceTests"));

        // 2. When tested directly via changed file line range
        var reportFile = await _service.AnalyzeImpactAsync(new TestImpactRequest
        {
            ChangedFiles = new List<string> { "src/Core/OrderService.cs" }
        });

        Assert.That(reportFile.AffectedTestMethods, Has.Count.EqualTo(1));
        Assert.That(reportFile.AffectedTestMethods[0].TestMethodName, Is.EqualTo("Test_ProcessOrder_EmitsEvent"));
        Assert.That(reportFile.AffectedTestMethods[0].Depth, Is.EqualTo(1));
    }

    [Test]
    public async Task Formatters_RenderMarkdownCleanly()
    {
        var fnTarget = new Node("fn:target", "Function", new Dictionary<string, object>
        {
            ["name"] = "ExecuteAction",
            ["file_path"] = "src/Target.cs",
            ["project"] = "App",
            ["start_line"] = 5,
            ["end_line"] = 15
        });

        var fnTest = new Node("fn:test_target", "Function", new Dictionary<string, object>
        {
            ["name"] = "Test_ExecuteAction_Ok",
            ["file_path"] = "tests/TargetTests.cs",
            ["project"] = "Tests",
            ["is_test"] = "true",
            ["test_framework"] = "xunit",
            ["start_line"] = 10,
            ["end_line"] = 20
        });

        await _db.UploadNodesAsync([fnTarget, fnTest]);
        var emptyProps = new Dictionary<string, object>();
        await _db.UploadRelationshipsAsync([
            new("fn:test_target", "fn:target", "CALLS", emptyProps)
        ]);

        var impactReport = await _service.AnalyzeImpactAsync(new TestImpactRequest
        {
            SymbolNames = new List<string> { "ExecuteAction" }
        });

        var mdImpact = TestIntelligenceService.FormatImpactMarkdown(impactReport);
        Assert.That(mdImpact, Does.Contain("Test Impact Analysis"));
        Assert.That(mdImpact, Does.Contain("Test_ExecuteAction_Ok"));
        Assert.That(mdImpact, Does.Contain("dotnet test"));

        var covReport = await _service.AnalyzeCoverageAsync(new TestCoverageFilter());
        var mdCov = TestIntelligenceService.FormatCoverageMarkdown(covReport);
        Assert.That(mdCov, Does.Contain("Test Coverage Report"));
    }

    [Test]
    public async Task AnalyzeCoverageAsync_FiltersByProjectAndStatus()
    {
        var proj1 = new Node("proj:p1", "Project", new Dictionary<string, object> { ["name"] = "P1", ["role"] = "Service", ["is_library"] = "false" });
        var proj2 = new Node("proj:p2", "Project", new Dictionary<string, object> { ["name"] = "P2", ["role"] = "Service", ["is_library"] = "false" });

        var type1 = new Node("type:t1", "Type", new Dictionary<string, object> { ["name"] = "Class1", ["project"] = "P1", ["kind"] = "Class", ["file_path"] = "src/P1/C1.cs", ["start_line"] = 1, ["end_line"] = 50 });
        var type2 = new Node("type:t2", "Type", new Dictionary<string, object> { ["name"] = "Class2", ["project"] = "P2", ["kind"] = "Class", ["file_path"] = "src/P2/C2.cs", ["start_line"] = 1, ["end_line"] = 50 });

        var fn1 = new Node("fn:f1", "Function", new Dictionary<string, object> { ["name"] = "M1", ["project"] = "P1", ["file_path"] = "src/P1/C1.cs", ["start_line"] = 5, ["end_line"] = 15 });
        var fn2 = new Node("fn:f2", "Function", new Dictionary<string, object> { ["name"] = "M2", ["project"] = "P2", ["file_path"] = "src/P2/C2.cs", ["start_line"] = 5, ["end_line"] = 15 });

        var test1 = new Node("fn:t1", "Function", new Dictionary<string, object> { ["name"] = "Test_M1", ["project"] = "P1Tests", ["is_test"] = "true", ["file_path"] = "tests/T1.cs", ["start_line"] = 5, ["end_line"] = 15 });

        await _db.UploadNodesAsync([proj1, proj2, type1, type2, fn1, fn2, test1]);
        var empty = new Dictionary<string, object>();
        await _db.UploadRelationshipsAsync([
            new("type:t1", "fn:f1", "HAS_METHOD", empty),
            new("type:t2", "fn:f2", "HAS_METHOD", empty),
            new("fn:t1", "fn:f1", "CALLS", empty)
        ]);

        // 1. Filter by Project P1
        var reportP1 = await _service.AnalyzeCoverageAsync(new TestCoverageFilter(Project: "P1"));
        Assert.That(reportP1.Summary.TotalClasses, Is.EqualTo(1));
        Assert.That(reportP1.CoveredClasses.Select(c => c.Name), Does.Contain("Class1"));
        Assert.That(reportP1.UncoveredClasses, Is.Empty);

        // 2. Filter by Status covered
        var reportCovered = await _service.AnalyzeCoverageAsync(new TestCoverageFilter(Status: "covered"));
        Assert.That(reportCovered.CoveredClasses, Has.Count.EqualTo(1));
        Assert.That(reportCovered.UncoveredClasses, Is.Empty);

        // 3. Filter by Status uncovered
        var reportUncovered = await _service.AnalyzeCoverageAsync(new TestCoverageFilter(Status: "uncovered"));
        Assert.That(reportUncovered.CoveredClasses, Is.Empty);
        Assert.That(reportUncovered.UncoveredClasses, Has.Count.EqualTo(1));
        Assert.That(reportUncovered.UncoveredClasses[0].Name, Is.EqualTo("Class2"));
    }

    [Test]
    public async Task AnalyzeImpactAsync_MultiFramework_GeneratesAppropriateCommands()
    {
        var fnPy = new Node("fn:py_target", "Function", new Dictionary<string, object> { ["name"] = "calc", ["file_path"] = "calc.py", ["project"] = "app", ["start_line"] = 1, ["end_line"] = 10 });
        var testPy = new Node("fn:py_test", "Function", new Dictionary<string, object> { ["name"] = "test_calc", ["file_path"] = "tests/test_calc.py", ["project"] = "app", ["is_test"] = "true", ["test_framework"] = "pytest", ["start_line"] = 1, ["end_line"] = 10 });

        var fnGo = new Node("fn:go_target", "Function", new Dictionary<string, object> { ["name"] = "Handle", ["file_path"] = "handler.go", ["project"] = "app", ["start_line"] = 1, ["end_line"] = 10 });
        var testGo = new Node("fn:go_test", "Function", new Dictionary<string, object> { ["name"] = "TestHandle", ["file_path"] = "handler_test.go", ["project"] = "app", ["is_test"] = "true", ["test_framework"] = "gotest", ["start_line"] = 1, ["end_line"] = 10 });

        await _db.UploadNodesAsync([fnPy, testPy, fnGo, testGo]);
        var empty = new Dictionary<string, object>();
        await _db.UploadRelationshipsAsync([
            new("fn:py_test", "fn:py_target", "CALLS", empty),
            new("fn:go_test", "fn:go_target", "CALLS", empty)
        ]);

        var report = await _service.AnalyzeImpactAsync(new TestImpactRequest
        {
            SymbolNames = new List<string> { "calc", "Handle" }
        });

        Assert.That(report.AffectedTestMethods, Has.Count.EqualTo(2));
        Assert.That(report.RunnerCommands, Contains.Key("pytest"));
        Assert.That(report.RunnerCommands["pytest"], Does.Contain("pytest tests/test_calc.py -k \"test_calc\""));

        Assert.That(report.RunnerCommands, Contains.Key("go"));
        Assert.That(report.RunnerCommands["go"], Does.Contain("go test ./... -run \"^(TestHandle)$\""));
    }

    [Test]
    public async Task AnalyzeImpactAsync_WhenAllTestsInClassAffected_CollapsesToClassNameFilter()
    {
        var typeCalc = new Node("type:calc_svc", "Type", new Dictionary<string, object>
        {
            ["name"] = "Calculator",
            ["file_path"] = "src/Calculator.cs",
            ["project"] = "App"
        });
        var fnAdd = new Node("fn:calc_add", "Function", new Dictionary<string, object>
        {
            ["name"] = "Add",
            ["file_path"] = "src/Calculator.cs",
            ["project"] = "App",
            ["start_line"] = 5,
            ["end_line"] = 10
        });

        var typeTest = new Node("type:calc_tests", "Type", new Dictionary<string, object>
        {
            ["name"] = "CalculatorTests",
            ["file_path"] = "tests/CalculatorTests.cs",
            ["project"] = "AppTests"
        });
        var test1 = new Node("fn:test_add1", "Function", new Dictionary<string, object>
        {
            ["name"] = "Test_Add_Positive",
            ["file_path"] = "tests/CalculatorTests.cs",
            ["project"] = "AppTests",
            ["is_test"] = "true",
            ["test_framework"] = "xunit",
            ["start_line"] = 10,
            ["end_line"] = 15
        });
        var test2 = new Node("fn:test_add2", "Function", new Dictionary<string, object>
        {
            ["name"] = "Test_Add_Negative",
            ["file_path"] = "tests/CalculatorTests.cs",
            ["project"] = "AppTests",
            ["is_test"] = "true",
            ["test_framework"] = "xunit",
            ["start_line"] = 20,
            ["end_line"] = 25
        });

        await _db.UploadNodesAsync([typeCalc, fnAdd, typeTest, test1, test2]);
        var empty = new Dictionary<string, object>();
        await _db.UploadRelationshipsAsync([
            new("type:calc_tests", "fn:test_add1", "HAS_METHOD", empty),
            new("type:calc_tests", "fn:test_add2", "HAS_METHOD", empty),
            new("fn:test_add1", "fn:calc_add", "CALLS", empty),
            new("fn:test_add2", "fn:calc_add", "CALLS", empty)
        ]);

        var report = await _service.AnalyzeImpactAsync(new TestImpactRequest
        {
            SymbolNames = new List<string> { "Add" }
        });

        Assert.That(report.AffectedTestMethods, Has.Count.EqualTo(2));
        Assert.That(report.Groups, Is.Not.Null);
        Assert.That(report.Groups!, Has.Count.EqualTo(1));

        var group = report.Groups![0];
        Assert.That(group.ClassName, Is.EqualTo("CalculatorTests"));
        Assert.That(group.AffectedTestCount, Is.EqualTo(2));
        Assert.That(group.TotalTestCount, Is.EqualTo(2));
        Assert.That(group.AllTestsAffected, Is.True);

        Assert.That(report.RunnerCommands, Contains.Key("dotnet"));
        Assert.That(report.RunnerCommands["dotnet"], Is.EqualTo("dotnet test --filter \"FullyQualifiedName~CalculatorTests\""));

        var md = TestIntelligenceService.FormatImpactMarkdown(report);
        Assert.That(md, Does.Contain("All tests affected"));
        Assert.That(md, Does.Contain("CalculatorTests"));
    }

    [Test]
    public async Task AnalyzeImpactAsync_WhenSubsetOfTestsInClassAffected_EmitsMethodLevelFilters()
    {
        var fnAdd = new Node("fn:calc_add", "Function", new Dictionary<string, object>
        {
            ["name"] = "Add",
            ["file_path"] = "src/Calculator.cs",
            ["project"] = "App",
            ["start_line"] = 5,
            ["end_line"] = 10
        });
        var fnSub = new Node("fn:calc_sub", "Function", new Dictionary<string, object>
        {
            ["name"] = "Sub",
            ["file_path"] = "src/Calculator.cs",
            ["project"] = "App",
            ["start_line"] = 15,
            ["end_line"] = 20
        });

        var typeTest = new Node("type:calc_tests", "Type", new Dictionary<string, object>
        {
            ["name"] = "CalculatorTests",
            ["file_path"] = "tests/CalculatorTests.cs",
            ["project"] = "AppTests"
        });
        var test1 = new Node("fn:test_add", "Function", new Dictionary<string, object>
        {
            ["name"] = "Test_Add",
            ["file_path"] = "tests/CalculatorTests.cs",
            ["project"] = "AppTests",
            ["is_test"] = "true",
            ["test_framework"] = "xunit",
            ["start_line"] = 10,
            ["end_line"] = 15
        });
        var test2 = new Node("fn:test_sub", "Function", new Dictionary<string, object>
        {
            ["name"] = "Test_Sub",
            ["file_path"] = "tests/CalculatorTests.cs",
            ["project"] = "AppTests",
            ["is_test"] = "true",
            ["test_framework"] = "xunit",
            ["start_line"] = 20,
            ["end_line"] = 25
        });

        await _db.UploadNodesAsync([fnAdd, fnSub, typeTest, test1, test2]);
        var empty = new Dictionary<string, object>();
        await _db.UploadRelationshipsAsync([
            new("type:calc_tests", "fn:test_add", "HAS_METHOD", empty),
            new("type:calc_tests", "fn:test_sub", "HAS_METHOD", empty),
            new("fn:test_add", "fn:calc_add", "CALLS", empty),
            new("fn:test_sub", "fn:calc_sub", "CALLS", empty)
        ]);

        // Modify only 'Add'
        var report = await _service.AnalyzeImpactAsync(new TestImpactRequest
        {
            SymbolNames = new List<string> { "Add" }
        });

        Assert.That(report.AffectedTestMethods, Has.Count.EqualTo(1));
        Assert.That(report.Groups, Is.Not.Null);
        Assert.That(report.Groups!, Has.Count.EqualTo(1));

        var group = report.Groups![0];
        Assert.That(group.ClassName, Is.EqualTo("CalculatorTests"));
        Assert.That(group.AffectedTestCount, Is.EqualTo(1));
        Assert.That(group.TotalTestCount, Is.EqualTo(2));
        Assert.That(group.AllTestsAffected, Is.False);

        Assert.That(report.RunnerCommands, Contains.Key("dotnet"));
        Assert.That(report.RunnerCommands["dotnet"], Is.EqualTo("dotnet test --filter \"FullyQualifiedName~Test_Add\""));
    }

    [Test]
    public async Task AnalyzeImpactAsync_IgnoresLifecycleAndHelperMethods_AsAffectedTests_WhileUsingHelpersAsBridges()
    {
        var fnTarget = new Node("fn:core_dowork", "Function", new Dictionary<string, object>
        {
            ["name"] = "DoWork",
            ["file_path"] = "src/Core/Worker.cs",
            ["project"] = "Core",
            ["start_line"] = 10,
            ["end_line"] = 20
        });

        var typeTest = new Node("type:worker_tests", "Type", new Dictionary<string, object>
        {
            ["name"] = "WorkerTests",
            ["file_path"] = "tests/WorkerTests.cs",
            ["project"] = "CoreTests"
        });

        // Lifecycle method (e.g. OneTimeSetUp) - not a test
        var fnSetUp = new Node("fn:setup", "Function", new Dictionary<string, object>
        {
            ["name"] = "OneTimeSetUp",
            ["file_path"] = "tests/WorkerTests.cs",
            ["project"] = "CoreTests",
            ["start_line"] = 5,
            ["end_line"] = 9
        });

        // Test helper method (RunCliAsync / helper) - not a test
        var fnHelper = new Node("fn:helper", "Function", new Dictionary<string, object>
        {
            ["name"] = "RunCliAsync",
            ["file_path"] = "tests/WorkerTests.cs",
            ["project"] = "CoreTests",
            ["start_line"] = 11,
            ["end_line"] = 18
        });

        // Real test
        var fnTest = new Node("fn:real_test", "Function", new Dictionary<string, object>
        {
            ["name"] = "Test_DoWork_Success",
            ["file_path"] = "tests/WorkerTests.cs",
            ["project"] = "CoreTests",
            ["is_test"] = "true",
            ["test_framework"] = "nunit",
            ["start_line"] = 20,
            ["end_line"] = 30
        });

        await _db.UploadNodesAsync([fnTarget, typeTest, fnSetUp, fnHelper, fnTest]);
        var empty = new Dictionary<string, object>();
        await _db.UploadRelationshipsAsync([
            new("type:worker_tests", "fn:setup", "HAS_METHOD", empty),
            new("type:worker_tests", "fn:helper", "HAS_METHOD", empty),
            new("type:worker_tests", "fn:real_test", "HAS_METHOD", empty),
            new("fn:setup", "fn:core_dowork", "CALLS", empty),
            new("fn:helper", "fn:core_dowork", "CALLS", empty),
            new("fn:real_test", "fn:helper", "CALLS", empty)
        ]);

        var report = await _service.AnalyzeImpactAsync(new TestImpactRequest
        {
            SymbolNames = new List<string> { "DoWork" },
            MaxDepth = 3
        });

        // Only Test_DoWork_Success should be in AffectedTestMethods
        Assert.That(report.AffectedTestMethods, Has.Count.EqualTo(1));
        var test = report.AffectedTestMethods[0];
        Assert.That(test.TestMethodName, Is.EqualTo("Test_DoWork_Success"));
        Assert.That(test.Depth, Is.EqualTo(2));
        Assert.That(string.Join(" → ", test.CallChain), Is.EqualTo("Test_DoWork_Success → RunCliAsync → DoWork"));

        // Groups: WorkerTests should have TotalTestCount = 1 (excluding OneTimeSetUp and RunCliAsync)
        Assert.That(report.Groups, Is.Not.Null);
        Assert.That(report.Groups!, Has.Count.EqualTo(1));
        var group = report.Groups![0];
        Assert.That(group.ClassName, Is.EqualTo("WorkerTests"));
        Assert.That(group.AffectedTestCount, Is.EqualTo(1));
        Assert.That(group.TotalTestCount, Is.EqualTo(1));
        Assert.That(group.AllTestsAffected, Is.True);

        // Command should target the class since all tests in it are affected
        Assert.That(report.RunnerCommands, Contains.Key("dotnet"));
        Assert.That(report.RunnerCommands["dotnet"], Is.EqualTo("dotnet test --filter \"FullyQualifiedName~WorkerTests\""));
    }
}

