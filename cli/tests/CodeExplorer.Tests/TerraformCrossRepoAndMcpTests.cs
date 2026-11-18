using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Cypher.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class TerraformCrossRepoAndMcpTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        WorkspaceIndexer.AutoRegisterParsers();
    }

    [Test]
    public async Task CrossRepoTerragruntAndTerraform_WiresInputsDependencyOutputsLambdaEnvAndCloudResourceLines()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ce-tf-crossrepo-" + Guid.NewGuid().ToString("N"));
        var frameworkTeamModDir = Path.Combine(tempRoot, "framework-team", "modules", "team");
        var frameworkAppModDir = Path.Combine(tempRoot, "framework-frontend-template", "modules", "p360");
        var bngTeamUnitDir = Path.Combine(tempRoot, "demo-bng", "live", "bngteam");
        var bngP360UnitDir = Path.Combine(tempRoot, "demo-bng", "live", "frontend-bng-p360");

        Directory.CreateDirectory(frameworkTeamModDir);
        Directory.CreateDirectory(frameworkAppModDir);
        Directory.CreateDirectory(bngTeamUnitDir);
        Directory.CreateDirectory(bngP360UnitDir);

        var dbPath = Path.Combine(tempRoot, "graph.db");

        try
        {
            await File.WriteAllTextAsync(Path.Combine(frameworkTeamModDir, "outputs.tf"), """
                resource "aws_athena_workgroup" "team" {
                  name = "bng-analytics-wg"
                }

                output "athena_workgroup_name" {
                  value = aws_athena_workgroup.team.name
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(frameworkAppModDir, "main.tf"), """
                variable "workgroup_name" {
                  type = string
                }

                variable "allowed_origin" {
                  type    = string
                  default = "*"
                }

                resource "aws_lambda_function" "api" {
                  function_name = "p360-api"
                  role          = "arn:aws:iam::123456789012:role/lambda"
                  handler       = "index.handler"
                  runtime       = "python3.12"

                  environment {
                    variables = {
                      ATHENA_WG      = var.workgroup_name
                      ALLOWED_ORIGIN = var.allowed_origin
                      STATIC_STAGE   = "prod"
                    }
                  }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(bngTeamUnitDir, "terragrunt.hcl"), """
                terraform {
                  source = "git::https://github.com/example/framework-team.git//modules/team?ref=v1.2.0"
                }

                inputs = {
                  team_name = "bng"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(bngP360UnitDir, "terragrunt.hcl"), """
                terraform {
                  source = "git::https://github.com/example/framework-frontend-template.git//modules/p360?ref=v1.0.0"
                }

                dependency "team_outputs" {
                  config_path = "../bngteam"
                }

                inputs = {
                  workgroup_name = dependency.team_outputs.outputs.athena_workgroup_name
                  ALLOWED_ORIGIN = "https://p360.bng.internal"
                }
                """);

            await using var client = new SqliteGraphClient(dbPath);
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(tempRoot, tempRoot, clear: true);

            using var repo = new CodeExplorerRepository(client, defaultWorkspacePath: tempRoot);

            // 1. CloudResource has non-null start_line and end_line in Cypher and get_file_outline
            var crQuery = await client.ExecuteCypherAsync(
                "MATCH (r:CloudResource) RETURN r.name AS name, r.start_line AS startLine, r.end_line AS endLine, r.line AS line, r.line_end AS lineEnd ORDER BY r.name");
            crQuery.Count.Should().Be(2);
            foreach (var row in crQuery.Rows)
            {
                row.GetIntProp("startLine").Should().BeGreaterThan(0);
                row.GetIntProp("endLine").Should().BeGreaterThanOrEqualTo(row.GetIntProp("startLine"));
                row.GetIntProp("line").Should().Be(row.GetIntProp("startLine"));
                row.GetIntProp("lineEnd").Should().Be(row.GetIntProp("endLine"));
            }

            var outlineYaml = await repo.GetFileOutlineAsync("framework-frontend-template/modules/p360/main.tf");
            outlineYaml.Should().Contain("aws_lambda_function.api");
            outlineYaml.Should().Contain("env.ATHENA_WG");
            outlineYaml.Should().Contain("env.ALLOWED_ORIGIN");

            // 2. Terragrunt inputs (including literal ALLOWED_ORIGIN) are indexed as Member nodes with initializer_expression
            var inputsQuery = await client.ExecuteCypherAsync(
                "MATCH (m:Member) WHERE m.name STARTS WITH 'inputs.' RETURN m.name AS name, m.initializer_expression AS initExpr, m.start_line AS startLine");
            var inputNames = inputsQuery.Rows.Select(r => r.GetStringProp("name")).ToList();
            inputNames.Should().Contain("inputs.workgroup_name");
            inputNames.Should().Contain("inputs.ALLOWED_ORIGIN");

            var allowedOriginRow = inputsQuery.Rows.First(r => r.GetStringProp("name") == "inputs.ALLOWED_ORIGIN");
            allowedOriginRow.GetStringProp("initExpr").Should().Be("https://p360.bng.internal");
            allowedOriginRow.GetIntProp("startLine").Should().BeGreaterThan(0);

            // 3. dependency.team_outputs.outputs.athena_workgroup_name attribute edge has output_name and reference_expression
            var depEdges = await client.ExecuteCypherAsync(
                "MATCH (src)-[r:DEPENDS_ON]->(d:Type {name: 'dependency.team_outputs'}) RETURN src.name AS srcName, r.output_name AS outName, r.reference_expression AS refExpr");
            depEdges.Count.Should().BeGreaterThan(0);
            depEdges.Rows.Should().Contain(r =>
                r.GetStringProp("srcName") == "inputs.workgroup_name" &&
                r.GetStringProp("outName") == "athena_workgroup_name" &&
                r.GetStringProp("refExpr") == "dependency.team_outputs.outputs.athena_workgroup_name");

            // 4. Direct edge from inputs.workgroup_name to output.athena_workgroup_name in framework-team across repos
            var crossRepoOutputEdges = await client.ExecuteCypherAsync(
                "MATCH (src:Member {name: 'inputs.workgroup_name'})-[r:DEPENDS_ON]->(out:Member {name: 'output.athena_workgroup_name'}) RETURN r.output_name AS outName");
            crossRepoOutputEdges.Count.Should().Be(1);
            crossRepoOutputEdges.Rows[0].GetStringProp("outName").Should().Be("athena_workgroup_name");

            // 5. Cross-repo Terragrunt inputs wire to framework-frontend-template var.workgroup_name
            var crossRepoVarEdges = await client.ExecuteCypherAsync(
                "MATCH (inp:Member {name: 'inputs.workgroup_name'})-[:CONFIGURES]->(v:Member {name: 'var.workgroup_name'}) RETURN v.file_path AS filePath");
            crossRepoVarEdges.Count.Should().Be(1);
            crossRepoVarEdges.Rows[0].GetStringProp("filePath").Should().EndWith("framework-frontend-template/modules/p360/main.tf");

            // 6. Lambda environment.variables wire CloudResource -> env.ATHENA_WG -> var.workgroup_name
            var lambdaEnvEdges = await client.ExecuteCypherAsync(
                "MATCH (cr:CloudResource {name: 'aws_lambda_function.api'})-[:DEPENDS_ON]->(env:Member {name: 'env.ATHENA_WG'})-[:DEPENDS_ON]->(v:Member {name: 'var.workgroup_name'}) RETURN env.initializer_expression AS initExpr");
            lambdaEnvEdges.Count.Should().Be(1);
            lambdaEnvEdges.Rows[0].GetStringProp("initExpr").Should().Be("var.workgroup_name");

            // 7. find_symbol finds Member (ALLOWED_ORIGIN, athena_workgroup_name) and CloudResource (aws_lambda_function.api) without symbolType
            var findAllowedOrigin = await repo.FindSymbolAsync("ALLOWED_ORIGIN");
            findAllowedOrigin.Should().Contain("ALLOWED_ORIGIN");

            var findLambda = await repo.FindSymbolAsync("aws_lambda_function.api");
            findLambda.Should().Contain("aws_lambda_function.api");

            // 8. fetch_code_snippets works with flow-style YAML (both top-level flow array and block sequence of flow objects)
            var flowArraySnippets = await repo.FetchCodeSnippetsAsync(
                "[{file_path: framework-frontend-template/modules/p360/main.tf, start_line: 10, end_line: 24}]");
            flowArraySnippets.Should().Contain("aws_lambda_function");
            flowArraySnippets.Should().Contain("ATHENA_WG");

            var blockFlowSnippets = await repo.FetchCodeSnippetsAsync(
                "- {file_path: demo-bng/live/frontend-bng-p360/terragrunt.hcl, line: 1, line_end: 14}");
            blockFlowSnippets.Should().Contain("athena_workgroup_name");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                try { Directory.Delete(tempRoot, recursive: true); } catch { }
            }
        }
    }
}
