using CodeExplorer.Parsers.Python;
using CodeExplorer.Parsers.Terraform.Hcl;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class PythonAndTerraformGrammarRegressionTests
{
    [SetUp]
    public void SetUp()
    {
        WorkspaceIndexer.AutoRegisterParsers();
    }

    [Test]
    public void PythonParser_Pep758UnparenthesizedExceptAndExceptStar_ProducesZeroDiagnostics()
    {
        const string source = """
            async def verify_live_publication(collection, realm_id: str, envelope):
                for attempt in range(3):
                    try:
                        return await verify_publication(collection, realm_id, envelope)
                    except PyMongoError, ConnectionError, TimeoutError:
                        if attempt < 2:
                            await asyncio.sleep(0.05)
                return None


            def authorize():
                try:
                    do_work()
                except KeyError, TypeError, ValueError, UnicodeError, RecursionError, OverflowError:
                    fallback()


            def handle_groups():
                try:
                    run_tasks()
                except* OSError, TimeoutError,:
                    recover()
            """;

        var doc = PythonSyntaxConverter.Instance.Parse(source, "python", "auth.py", "auth.py");
        doc.Should().NotBeNull();
        doc!.Diagnostics.Should().BeEmpty();
    }

    [Test]
    public void PythonParser_Pep646StarredExpressionInsideSubscript_ProducesZeroDiagnostics()
    {
        const string source = """
            import numpy as np
            import pandas as pd

            def build_frame():
                return pd.DataFrame(
                    {
                        "days": np.r_[
                            *[
                                range(1, 19)
                                for customer in range(4)
                                for year in [2018, 2019, 2020, 2021, 2022]
                            ]
                        ],
                    }
                )
            """;

        var doc = PythonSyntaxConverter.Instance.Parse(source, "python", "frame.py", "frame.py");
        doc.Should().NotBeNull();
        doc!.Diagnostics.Should().BeEmpty();
    }

    [Test]
    public void HclParser_MultilineTupleAndForComprehensionBinaryContinuations_ProducesZeroDiagnostics()
    {
        const string repro1 = """
            run "test_remotes" {
              assert {
                condition = output.remote_repository_keys_by_service["TOS"]["gradle"] == [
                  "transportation-optimization-service-gdlcentral-remote",
                  "transportation-optimization-service-gdlcflt-remote",
                  ] && output.virtual_remote_repository_keys_by_service["TOS"]["gradle"] == [
                  "transportation-optimization-service-gdlcentral-remote",
                  "transportation-optimization-service-gdlcflt-remote",
                ] && artifactory_remote_gradle_repository.service["TOS|gradle|maven-central"].key == "transportation-optimization-service-gdlcentral-remote"
                error_message = "Gradle remotes must match."
              }
            }
            """;

        const string repro2 = """
            run "test_policies" {
              assert {
                condition = toset([
                  for policy in xray_watch.service_policy["Checkout-Service|high_medium"].assigned_policy : policy.name
                  ]) == toset([
                  "global-remotes-malicious-watch",
                  "global-remotes-critical-watch",
                  "global-remotes-high-watch",
                  "global-remotes-medium-watch",
                ])
                error_message = "Watch policies must match."
              }
            }
            """;

        HclParser.TryParse(repro1, "remotes.tftest.hcl", out var file1).Should().BeTrue();
        file1.Diagnostics.Should().BeEmpty();
        file1.Blocks.Should().HaveCount(1);

        HclParser.TryParse(repro2, "policies.tftest.hcl", out var file2).Should().BeTrue();
        file2.Diagnostics.Should().BeEmpty();
        file2.Blocks.Should().HaveCount(1);
    }

    [Test]
    public async Task WorkspaceIndexAndDoctor_AllThreeBlueYonderRepros_ProduceZeroScanIssuesAndHonorTimeout()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce_by_repro_" + Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(tempDir, ".codeexplorer", "graph.db");
        Directory.CreateDirectory(Path.Combine(tempDir, "py_service"));
        Directory.CreateDirectory(Path.Combine(tempDir, "tf_infra", "tests"));
        Directory.CreateDirectory(Path.Combine(tempDir, ".codeexplorer"));

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "py_service", "pyproject.toml"), """
                [project]
                name = "py-service"
                version = "0.1.0"
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "py_service", "service.py"), """
                import numpy as np
                import pandas as pd

                async def verify_live_publication(collection, realm_id: str, envelope):
                    for attempt in range(3):
                        try:
                            return await verify_publication(collection, realm_id, envelope)
                        except PyMongoError, ConnectionError, TimeoutError:
                            if attempt < 2:
                                await asyncio.sleep(0.05)
                    return None

                def build_frame():
                    return pd.DataFrame(
                        {
                            "days": np.r_[
                                *[
                                    range(1, 19)
                                    for customer in range(4)
                                    for year in [2018, 2019, 2020, 2021, 2022]
                                ]
                            ],
                        }
                    )
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "tf_infra", "main.tf"), """
                resource "artifactory_remote_gradle_repository" "service" {
                  key = "transportation-optimization-service-gdlcentral-remote"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(tempDir, "tf_infra", "tests", "suite.tftest.hcl"), """
                run "test_remotes" {
                  assert {
                    condition = output.remote_repository_keys_by_service["TOS"]["gradle"] == [
                      "transportation-optimization-service-gdlcentral-remote",
                      "transportation-optimization-service-gdlcflt-remote",
                      ] && output.virtual_remote_repository_keys_by_service["TOS"]["gradle"] == [
                      "transportation-optimization-service-gdlcentral-remote",
                      "transportation-optimization-service-gdlcflt-remote",
                    ] && artifactory_remote_gradle_repository.service["TOS|gradle|maven-central"].key == "transportation-optimization-service-gdlcentral-remote"
                    error_message = "Gradle remotes must match."
                  }
                }

                run "test_policies" {
                  assert {
                    condition = toset([
                      for policy in xray_watch.service_policy["Checkout-Service|high_medium"].assigned_policy : policy.name
                      ]) == toset([
                      "global-remotes-malicious-watch",
                      "global-remotes-critical-watch",
                      "global-remotes-high-watch",
                      "global-remotes-medium-watch",
                    ])
                    error_message = "Watch policies must match."
                  }
                }
                """);

            await using var client = new SqliteGraphClient(dbPath);
            var originalTimeout = client.CommandTimeoutSeconds;
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempDir, clear: true);

            var doctor = new WorkspaceDoctorService();
            var report = await doctor.AnalyzeAsync(client, tempDir, new WorkspaceDoctorOptions
            {
                IncludeIssues = true,
                IncludeGaps = true,
                QueryTimeoutSeconds = 120
            });

            client.CommandTimeoutSeconds.Should().Be(originalTimeout);
            report.Issues.Should().NotBeNull();
            report.Issues!.TotalCount.Should().Be(0);
            report.Gaps.Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
