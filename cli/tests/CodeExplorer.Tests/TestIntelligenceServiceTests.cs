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

        hunks.Should().HaveCount(2);
        hunks[0].File.Should().Be("src/Core/UserService.cs");
        hunks[0].StartLine.Should().Be(45);
        hunks[0].EndLine.Should().Be(52);

        hunks[1].File.Should().Be("src/Core/UserService.cs");
        hunks[1].StartLine.Should().Be(122);
        hunks[1].EndLine.Should().Be(126);
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

        report.Should().NotBeNull();
        report.Summary.TotalClasses.Should().Be(2);
        report.Summary.CoveredClasses.Should().Be(1);
        report.Summary.UncoveredClasses.Should().Be(1);
        report.Summary.ClassCoveragePercentage.Should().Be(50.0);

        report.Summary.TotalMethods.Should().Be(3);
        report.Summary.CoveredMethods.Should().Be(1);
        report.Summary.UncoveredMethods.Should().Be(2);
        Math.Round(report.Summary.MethodCoveragePercentage, 1).Should().Be(33.3);

        report.CoveredClasses.Should().HaveCount(1);
        report.CoveredClasses[0].Name.Should().Be("UserService");
        report.CoveredClasses[0].CoveredMethodsCount.Should().Be(1);

        report.UncoveredClasses.Should().HaveCount(1);
        report.UncoveredClasses[0].Name.Should().Be("OrderService");
        report.UncoveredClasses[0].MethodCount.Should().Be(1);
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

        reportRepo.AffectedTestMethods.Should().HaveCount(1);
        var test = reportRepo.AffectedTestMethods[0];
        test.TestMethodName.Should().Be("Test_ProcessOrder_EmitsEvent");
        test.Depth.Should().Be(2);
        test.TargetSymbol.Should().Be("SaveEntity");
        test.TestFramework.Should().Be("nunit");
        reportRepo.RunnerCommands.Should().ContainKey("dotnet");
        reportRepo.RunnerCommands["dotnet"].Should().Contain("OrderServiceTests");

        // 2. When tested directly via changed file line range
        var reportFile = await _service.AnalyzeImpactAsync(new TestImpactRequest
        {
            ChangedFiles = new List<string> { "src/Core/OrderService.cs" }
        });

        reportFile.AffectedTestMethods.Should().HaveCount(1);
        reportFile.AffectedTestMethods[0].TestMethodName.Should().Be("Test_ProcessOrder_EmitsEvent");
        reportFile.AffectedTestMethods[0].Depth.Should().Be(1);
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
        mdImpact.Should().Contain("Test Impact Analysis");
        mdImpact.Should().Contain("Test_ExecuteAction_Ok");
        mdImpact.Should().Contain("dotnet test");

        var covReport = await _service.AnalyzeCoverageAsync(new TestCoverageFilter());
        var mdCov = TestIntelligenceService.FormatCoverageMarkdown(covReport);
        mdCov.Should().Contain("Test Coverage Report");
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
        reportP1.Summary.TotalClasses.Should().Be(1);
        reportP1.CoveredClasses.Select(c => c.Name).Should().Contain("Class1");
        reportP1.UncoveredClasses.Should().BeEmpty();

        // 2. Filter by Status covered
        var reportCovered = await _service.AnalyzeCoverageAsync(new TestCoverageFilter(Status: "covered"));
        reportCovered.CoveredClasses.Should().HaveCount(1);
        reportCovered.UncoveredClasses.Should().BeEmpty();

        // 3. Filter by Status uncovered
        var reportUncovered = await _service.AnalyzeCoverageAsync(new TestCoverageFilter(Status: "uncovered"));
        reportUncovered.CoveredClasses.Should().BeEmpty();
        reportUncovered.UncoveredClasses.Should().HaveCount(1);
        reportUncovered.UncoveredClasses[0].Name.Should().Be("Class2");
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

        report.AffectedTestMethods.Should().HaveCount(2);
        report.RunnerCommands.Should().ContainKey("pytest");
        report.RunnerCommands["pytest"].Should().Contain("pytest tests/test_calc.py -k \"test_calc\"");

        report.RunnerCommands.Should().ContainKey("go");
        report.RunnerCommands["go"].Should().Contain("go test ./... -run \"^(TestHandle)$\"");
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

        report.AffectedTestMethods.Should().HaveCount(2);
        report.Groups.Should().NotBeNull();
        report.Groups!.Should().HaveCount(1);

        var group = report.Groups![0];
        group.ClassName.Should().Be("CalculatorTests");
        group.AffectedTestCount.Should().Be(2);
        group.TotalTestCount.Should().Be(2);
        group.AllTestsAffected.Should().BeTrue();

        report.RunnerCommands.Should().ContainKey("dotnet");
        report.RunnerCommands["dotnet"].Should().Be("dotnet test --filter \"FullyQualifiedName~CalculatorTests\"");

        var md = TestIntelligenceService.FormatImpactMarkdown(report);
        md.Should().Contain("All tests affected");
        md.Should().Contain("CalculatorTests");
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

        report.AffectedTestMethods.Should().HaveCount(1);
        report.Groups.Should().NotBeNull();
        report.Groups!.Should().HaveCount(1);

        var group = report.Groups![0];
        group.ClassName.Should().Be("CalculatorTests");
        group.AffectedTestCount.Should().Be(1);
        group.TotalTestCount.Should().Be(2);
        group.AllTestsAffected.Should().BeFalse();

        report.RunnerCommands.Should().ContainKey("dotnet");
        report.RunnerCommands["dotnet"].Should().Be("dotnet test --filter \"FullyQualifiedName~Test_Add\"");
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
        report.AffectedTestMethods.Should().HaveCount(1);
        var test = report.AffectedTestMethods[0];
        test.TestMethodName.Should().Be("Test_DoWork_Success");
        test.Depth.Should().Be(2);
        string.Join(" → ", test.CallChain).Should().Be("Test_DoWork_Success → RunCliAsync → DoWork");

        // Groups: WorkerTests should have TotalTestCount = 1 (excluding OneTimeSetUp and RunCliAsync)
        report.Groups.Should().NotBeNull();
        report.Groups!.Should().HaveCount(1);
        var group = report.Groups![0];
        group.ClassName.Should().Be("WorkerTests");
        group.AffectedTestCount.Should().Be(1);
        group.TotalTestCount.Should().Be(1);
        group.AllTestsAffected.Should().BeTrue();

        // Command should target the class since all tests in it are affected
        report.RunnerCommands.Should().ContainKey("dotnet");
        report.RunnerCommands["dotnet"].Should().Be("dotnet test --filter \"FullyQualifiedName~WorkerTests\"");
    }
}

