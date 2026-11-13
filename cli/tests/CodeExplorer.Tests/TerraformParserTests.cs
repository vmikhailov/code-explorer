using System.Threading.Channels;
using AwesomeAssertions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.Terraform;
using CodeExplorer.Parser.Terraform.Hcl;
using NUnit.Framework;

namespace CodeExplorer.Tests;

/// <summary>
/// Drives the Terraform parser + Layer 4 enricher over inline HCL fixtures and asserts on the
/// declarations, DEPENDS_ON/DEPLOYS edges, and semantic nodes they produce. Each fixture doubles
/// as a regression guard for a specific behavior (declarations, reference anchoring, cloud mapping)
/// and for the review findings R2/R4/R5/R9.
/// </summary>
[TestFixture]
public class TerraformParserTests
{
    // Captured graph from parsing one or more HCL files in a single directory and enriching.
    private sealed record EnrichResult(
        FileNode FileNode,
        IReadOnlyList<IOntologyNode> SemanticNodes,
        IReadOnlyList<Relationship> Relationships);

    // Writes the files into a temp project dir, parses each, runs the enricher for that directory,
    // drains the context channel, and returns the declarations, semantic nodes, and edges.
    private static async Task<EnrichResult> ParseAndEnrichAsync(params (string Name, string Content)[] files)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-tf-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var semantic = new SemanticStructureNode("ws:semantic", "semantic", string.Empty);
            var ctx = new ParsingContext(tempDir, tempDir, client, channel)
            {
                SemanticStructure = semantic
            };

            var parser = new TerraformParser();
            FileNode firstFileNode = null!;

            foreach (var (name, content) in files)
            {
                var filePath = Path.Combine(tempDir, name);
                await File.WriteAllTextAsync(filePath, content);
                using var tree = await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
                firstFileNode ??= tree.FileNode;
            }

            // The project directory is the workspace root here (flat fixture).
            var projectNode = new ProjectNode($"{ctx.WorkspaceId}:p::", "proj", string.Empty, "hcl", null);
            var enricher = new TerraformSyntaxEnricher(parser);
            await enricher.EnrichAsync(projectNode, ctx);

            // Flush enqueued relationship uploads into the in-memory client.
            channel.Writer.Complete();
            while (channel.Reader.TryRead(out var work))
            {
                await work();
            }

            return new EnrichResult(firstFileNode, semantic.Children, client.UploadedRelationships);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    private static IEnumerable<IOntologyNode> Flatten(IOntologyNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var d in Flatten(child)) yield return d;
        }
    }

    [Test]
    public async Task Declarations_ResourceVariableOutputLocal_ProduceTypedNodes()
    {
        var result = await ParseAndEnrichAsync(("main.tf", """
            variable "env" {
              type = string
            }

            locals {
              prefix = "app-${var.env}"
            }

            resource "aws_s3_bucket" "data" {
              bucket = "${local.prefix}-data"
            }

            output "bucket_id" {
              value = aws_s3_bucket.data.id
            }
            """));

        var nodes = Flatten(result.FileNode).ToList();

        nodes.OfType<TypeNode>().Should().ContainSingle(t => t.Name == "aws_s3_bucket.data");
        nodes.OfType<MemberNode>().Select(m => m.Name)
            .Should().Contain(new[] { "env", "prefix", "bucket_id" });
    }

    [Test]
    public async Task ResourceReference_AnchorsAtResourceNode_NotFile()
    {
        // E1: the DEPENDS_ON edge for a reference made inside a resource must originate at the
        // resource declaration, not the file node.
        var result = await ParseAndEnrichAsync(("main.tf", """
            variable "env" { type = string }

            resource "aws_sqs_queue" "jobs" {
              name = var.env
            }
            """));

        var edge = result.Relationships.Should()
            .ContainSingle(r => r.Kind == OntologyConstants.Relationships.DependsOn
                && r.Properties.ContainsKey("ref") && (string)r.Properties["ref"] == "var")
            .Subject;

        // The source is the resource's declaration URN (…:Type:aws_sqs_queue.jobs:…), not the file.
        edge.From.Should().Contain(":Type:aws_sqs_queue.jobs");
        edge.From.Should().NotContain(":File:");
    }

    [Test]
    public async Task OutputReference_AnchorsAtOutputNode_NotFile()
    {
        // R4: references inside an output block anchor at the output member node.
        var result = await ParseAndEnrichAsync(("main.tf", """
            resource "aws_vpc" "main" {
              cidr_block = "10.0.0.0/16"
            }

            output "vpc_id" {
              value = aws_vpc.main.id
            }
            """));

        var edge = result.Relationships.Should()
            .ContainSingle(r => r.Kind == OntologyConstants.Relationships.DependsOn
                && r.To.Contains("aws_vpc.main"))
            .Subject;

        // The source is the output's member declaration URN (…:Member:vpc_id:…), not the file node.
        edge.From.Should().Contain(":Member:vpc_id");
        edge.From.Should().NotContain(":File:");
    }

    [Test]
    public async Task ModuleOutputs_DistinctReads_EachProduceAnEdge()
    {
        // R9: two different outputs of the same module must each survive as a distinct edge.
        var result = await ParseAndEnrichAsync(("main.tf", """
            module "network" {
              source = "./modules/network"
            }

            resource "aws_instance" "app" {
              subnet_id = module.network.subnet_id
              vpc_id    = module.network.vpc_id
            }
            """));

        var moduleOuts = result.Relationships
            .Where(r => r.Kind == OntologyConstants.Relationships.DependsOn
                && r.Properties.ContainsKey("ref") && (string)r.Properties["ref"] == "module"
                && r.Properties.ContainsKey("out"))
            .Select(r => (string)r.Properties["out"])
            .ToList();

        // Both distinct module outputs survive as separate edges out of the enricher (R9). The
        // persisted SQLite graph additionally dedups edges by (from, to, kind), so these two
        // collapse there; see KNOWN_ISSUES R9 note. This test asserts the enricher's output.
        moduleOuts.Should().Contain(new[] { "subnet_id", "vpc_id" });
    }

    [Test]
    public async Task SnsTopic_MapsToTopicNode_WorkspaceGlobalId()
    {
        // P4: an SNS topic becomes a Topic semantic node; its id is workspace-global (no dir scope)
        // so app code and the IaC reconcile.
        var result = await ParseAndEnrichAsync(("sns.tf", """
            resource "aws_sns_topic" "events" {
              name = "team-events"
            }
            """));

        var topic = result.SemanticNodes.OfType<TopicNode>().Should()
            .ContainSingle(t => t.Name == "events").Subject;
        topic.Id.Should().Contain(":top:");
    }

    [Test]
    public async Task CloudServiceId_IsScopedByDirectory()
    {
        // R5: the CloudService fallback id carries a directory scope segment so same-named
        // plumbing resources in different modules stay distinct.
        var result = await ParseAndEnrichAsync(("iam.tf", """
            resource "aws_iam_role" "exec" {
              name = "exec-role"
            }
            """));

        var svc = result.SemanticNodes.OfType<CloudServiceNode>().Should()
            .ContainSingle(s => s.Name == "exec").Subject;

        // Flat fixture → "root" scope segment between the resource type and the name.
        svc.Id.Should().Contain(":cloud:aws_iam_role:root:exec");
    }

    [Test]
    public async Task LockFile_ProducesNoDeclarations()
    {
        // R2: a .terraform.lock.hcl must not produce provider declarations named after registry URLs.
        var result = await ParseAndEnrichAsync((".terraform.lock.hcl", """
            provider "registry.terraform.io/hashicorp/aws" {
              version = "5.0.0"
            }
            """));

        var nodes = Flatten(result.FileNode).ToList();
        nodes.OfType<TypeNode>().Should().BeEmpty();
        nodes.Should().NotContain(n => n.Id.Contains("registry.terraform.io"));
    }

    [Test]
    public async Task Tfvars_ConfiguresVariable_SameDir()
    {
        // S1: a .tfvars assignment links to the declared variable in the same directory, modeled as
        // DEPENDS_ON with via=tfvars, ref=configures (see DESIGN_DECISIONS.md D1).
        var result = await ParseAndEnrichAsync(
            ("variables.tf", """
                variable "env" { type = string }
                """),
            ("prod.tfvars", """
                env = "prod"
                """));

        var edge = result.Relationships.Should()
            .ContainSingle(r => r.Kind == OntologyConstants.Relationships.DependsOn
                && r.Properties.ContainsKey("ref") && (string)r.Properties["ref"] == "configures")
            .Subject;

        edge.From.Should().Contain("prod.tfvars");
        edge.To.Should().Contain(":Member:env");
        ((string)edge.Properties["via"]).Should().Be("tfvars");
    }

    [Test]
    public void InputsObjectKeys_IgnoreTernaryBranch_KeepRealKeys()
    {
        // N5: a ternary value (cond ? foo : bar) must not make `foo` look like an input key, while
        // real keys — whether written `key =` or `key:` — are still captured.
        var hcl = """
            inputs = {
              env     = "prod"
              region: "us-east-1"
              enabled = var.flag ? alpha : beta
            }
            """;
        var ast = new HclParser(new HclLexer(hcl).Tokenize()).ParseFile();
        var keys = TerraformDeclarationMapper.MapConfigures(ast, "live/app/terragrunt.hcl")
            .Select(c => c.VariableName)
            .ToList();

        keys.Should().BeEquivalentTo(new[] { "env", "region", "enabled" });
        keys.Should().NotContain("alpha");
        keys.Should().NotContain("beta");
    }

    [Test]
    public async Task TerragruntInputs_ConfigureWrappedModuleVariables_CrossDir()
    {
        // S2: a Terragrunt unit's inputs configure the wrapped module's variables, resolved via the
        // unit's terraform { source }. Cross-dir, so a dedicated multi-project setup is used.
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-tf-s2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempDir, "modules", "app"));
        Directory.CreateDirectory(Path.Combine(tempDir, "live", "app"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "modules", "app", "variables.tf"),
                "variable \"env\" { type = string }\nvariable \"region\" { type = string }\n");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "live", "app", "terragrunt.hcl"),
                "terraform {\n  source = \"../../modules/app\"\n}\n\ninputs = {\n  env    = \"demo\"\n  region = \"us-east-1\"\n}\n");

            var channel = Channel.CreateUnbounded<Func<Task>>();
            await using var client = new InMemoryGraphRepository();
            var ctx = new ParsingContext(tempDir, tempDir, client, channel)
            {
                SemanticStructure = new SemanticStructureNode("ws:semantic", "semantic", string.Empty)
            };
            var parser = new TerraformParser();
            foreach (var rel in new[] { "modules/app/variables.tf", "live/app/terragrunt.hcl" })
            {
                using var tree = await parser.ParseAsync(Path.Combine(tempDir, rel), "p", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath);
            }
            foreach (var dir in new[] { "modules/app", "live/app" })
            {
                var pn = new ProjectNode($"{ctx.WorkspaceId}:p:{dir}:", "p", dir, "hcl", null);
                await new TerraformSyntaxEnricher(parser).EnrichAsync(pn, ctx);
            }
            channel.Writer.Complete();
            while (channel.Reader.TryRead(out var work)) await work();

            var configuresEdges = client.UploadedRelationships
                .Where(r => r.Kind == OntologyConstants.Relationships.DependsOn
                    && r.Properties.ContainsKey("ref") && (string)r.Properties["ref"] == "configures"
                    && (string)r.Properties["via"] == "terragrunt")
                .ToList();

            configuresEdges.Should().HaveCount(2);
            configuresEdges.Should().OnlyContain(r => r.From.Contains("live/app/terragrunt.hcl"));
            configuresEdges.Select(r => r.To).Should()
                .Contain(t => t.Contains(":Member:env"))
                .And.Contain(t => t.Contains(":Member:region"));
            configuresEdges.Should().OnlyContain(r => r.Properties.ContainsKey("inferred") && (bool)r.Properties["inferred"]);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task HyphenatedName_InInterpolation_HarvestsFullName_NoTruncation()
    {
        // N1: a hyphenated reference inside a ${...} interpolation must harvest the full name
        // (var.my-flag), not truncate at the hyphen (var.my) — which would miss the edge or, with
        // the decoy variable "my" present, resolve to the wrong node.
        var result = await ParseAndEnrichAsync(("main.tf", """
            variable "my-flag" { type = bool }
            variable "my" { type = string }

            resource "aws_s3_bucket" "b" {
              bucket = "prefix-${var.my-flag}-suffix"
            }
            """));

        var refEdges = result.Relationships
            .Where(r => r.Kind == OntologyConstants.Relationships.DependsOn
                && r.Properties.TryGetValue("ref", out var k) && (string)k == "var")
            .ToList();

        // The edge resolves to variable "my-flag", not the truncated decoy "my".
        refEdges.Should().Contain(r => r.To.Contains(":Member:my-flag"));
        refEdges.Should().NotContain(r => r.To.EndsWith(":Member:my:1") || r.To.Contains(":Member:my:"));
    }

    [Test]
    public async Task EscapedInterpolation_ProducesNoReferenceEdge()
    {
        // F1: "$${var.foo}" is an escaped literal "${var.foo}", not an interpolation, so it must
        // NOT produce a reference edge even when variable "foo" is declared. A real "${var.foo}"
        // does produce one.
        var result = await ParseAndEnrichAsync(("main.tf", """
            variable "foo" { type = string }

            resource "aws_s3_bucket" "escaped" {
              bucket = "$${var.foo}"
            }

            resource "aws_s3_bucket" "live" {
              bucket = "${var.foo}"
            }
            """));

        var varFooEdges = result.Relationships
            .Where(r => r.Kind == OntologyConstants.Relationships.DependsOn && r.To.Contains(":Member:foo"))
            .ToList();

        // Exactly one edge — from the live interpolation resource, not the escaped one.
        varFooEdges.Should().ContainSingle();
        varFooEdges[0].From.Should().Contain(":Type:aws_s3_bucket.live");
        varFooEdges.Should().NotContain(r => r.From.Contains(":Type:aws_s3_bucket.escaped"));
    }

    [Test]
    public async Task AppInfraReconciliation_TerraformDbUsesCanonicalId()
    {
        // Spec §5.6 headline feature depends on the Terraform enricher emitting a Database node whose
        // id matches the canonical scheme app/config parsers target (ws:db:{dbType}:{name}), so the
        // two merge by shared id. This guards that contract — the thing that would silently break
        // reconciliation. (End-to-end merge with an app config verified manually; see D6.)
        var root = Path.Combine(Path.GetTempPath(), "ce-tf-recon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "infra"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "infra", "main.tf"), """
                resource "aws_db_instance" "orders" {
                  identifier = "orders"
                  name       = "orders"
                  engine     = "postgres"
                }
                """);

            WorkspaceIndexer.AutoRegisterParsers();
            await using var client = new InMemoryGraphRepository();
            var indexer = new WorkspaceIndexer(client);
            await indexer.IndexAsync(root, clear: false);

            var dbNodes = client.UploadedNodes
                .Where(n => n.Kind == OntologyConstants.NodeLabels.Database
                    && n.Properties.GetValueOrDefault("name")?.ToString() == "orders")
                .DistinctBy(n => n.Id)
                .ToList();
            dbNodes.Should().ContainSingle();

            // The canonical reconciliation id: {ws}:{Database-prefix}:{dbType}:{name}, no extra scope.
            var dbId = dbNodes[0].Id;
            dbId.Should().EndWith($":{OntologyConstants.IdPrefixes.Database}:relational:orders");

            // The Terraform resource provisions exactly that node.
            client.UploadedRelationships.Should().Contain(r =>
                r.Kind == OntologyConstants.Relationships.Deploys
                && r.To == dbId && r.From.Contains("aws_db_instance.orders"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task DependsOn_TagsEdgeRefExplicit()
    {
        // S5: a depends_on meta-argument yields ref=explicit, distinct from an implicit data ref.
        var result = await ParseAndEnrichAsync(("main.tf", """
            resource "aws_iam_role" "r" { name = "role" }

            resource "aws_instance" "app" {
              ami        = "ami-123"
              depends_on = [aws_iam_role.r]
            }
            """));

        var edge = result.Relationships.Should()
            .ContainSingle(r => r.Kind == OntologyConstants.Relationships.DependsOn
                && r.To.Contains("aws_iam_role.r"))
            .Subject;
        ((string)edge.Properties["ref"]).Should().Be("explicit");
        edge.From.Should().Contain(":Type:aws_instance.app");
    }

    [Test]
    public async Task DataSourceReference_ResolvesWithRefData()
    {
        // A data.<type>.<name> reference resolves to the data declaration with ref=data.
        var result = await ParseAndEnrichAsync(("main.tf", """
            data "aws_ami" "ubuntu" {
              most_recent = true
            }

            resource "aws_instance" "app" {
              ami = data.aws_ami.ubuntu.id
            }
            """));

        var edge = result.Relationships.Should()
            .ContainSingle(r => r.Kind == OntologyConstants.Relationships.DependsOn
                && r.To.Contains("aws_ami.ubuntu"))
            .Subject;
        ((string)edge.Properties["ref"]).Should().Be("data");
        edge.From.Should().Contain(":Type:aws_instance.app");
    }
}
