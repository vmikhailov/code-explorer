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
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parser.Terraform;
using NUnit.Framework;

namespace CodeExplorer.Tests;

/// <summary>
/// Drives the Terraform parser + Layer 4 enricher over inline HCL fixtures and asserts on the
/// declarations, DEPENDS_ON/CONFIGURES/DEPLOYS edges, and semantic nodes they produce. Each
/// fixture guards one behavior (declarations, reference anchoring and resolution, cloud mapping).
/// </summary>
[TestFixture]
public class TerraformParserTests
{
    // Captured graph from parsing one or more HCL files in a single directory and enriching.
    private sealed record EnrichResult(
        FileNode FileNode,
        IReadOnlyList<IOntologyNode> SemanticNodes,
        IReadOnlyList<Relationship> Relationships);

    // Writes the files into a temp project dir, parses each, runs the enricher for that directory
    // over the parsed trees, drains the context channel, and returns the declarations, semantic
    // nodes, and edges.
    private static Task<EnrichResult> ParseAndEnrichAsync(params (string Name, string Content)[] files) =>
        ParseAndEnrichInDirAsync(string.Empty, files);

    // As ParseAndEnrichAsync, with the files and project in module directory `projectDir`
    // (workspace-relative, "" for the root).
    private static async Task<EnrichResult> ParseAndEnrichInDirAsync(string projectDir, params (string Name, string Content)[] files)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ce-tf-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var trees = new List<SyntaxTree>();
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

            foreach (var (name, content) in files)
            {
                var filePath = Path.Combine(tempDir, projectDir, name);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                await File.WriteAllTextAsync(filePath, content);
                trees.Add(await parser.ParseAsync(filePath, "parent-id", ctx.WorkspaceId, ctx.AbsoluteWorkspacePath));
            }

            var projectNode = new ProjectNode($"{ctx.WorkspaceId}:p:{projectDir}:", "proj", projectDir, "hcl", null);
            await parser.EnrichProjectAsync(projectNode, trees, ctx);

            // Flush enqueued relationship uploads into the in-memory client.
            channel.Writer.Complete();
            while (channel.Reader.TryRead(out var work))
            {
                await work();
            }

            return new EnrichResult(trees[0].FileNode, semantic.Children, client.UploadedRelationships);
        }
        finally
        {
            foreach (var tree in trees) tree.Dispose();
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
        // A reference made inside a resource produces a DEPENDS_ON edge from the resource
        // declaration, not from the file node.
        var result = await ParseAndEnrichAsync(("main.tf", """
            variable "env" { type = string }

            resource "aws_sqs_queue" "jobs" {
              name = var.env
            }
            """));

        var edge = result.Relationships.Should()
            .ContainSingle(r => r.Kind == RelationshipKind.DependsOn && r.To.Contains(":Member:env"))
            .Subject;

        // The source is the resource's declaration URN (…:Type:aws_sqs_queue.jobs:…), not the file.
        edge.From.Should().Contain(":Type:aws_sqs_queue.jobs");
        edge.From.Should().NotContain(":File:");
        edge.Via.Should().Be("terraform");
        edge.Line.Should().Be(4);
    }

    [Test]
    public async Task OutputReference_AnchorsAtOutputNode_NotFile()
    {
        // References inside an output block anchor at the output member node.
        var result = await ParseAndEnrichAsync(("main.tf", """
            resource "aws_vpc" "main" {
              cidr_block = "10.0.0.0/16"
            }

            output "vpc_id" {
              value = aws_vpc.main.id
            }
            """));

        var edge = result.Relationships.Should()
            .ContainSingle(r => r.Kind == RelationshipKind.DependsOn
                && r.To.Contains("aws_vpc.main"))
            .Subject;

        // The source is the output's member declaration URN (…:Member:vpc_id:…), not the file node.
        edge.From.Should().Contain(":Member:vpc_id");
        edge.From.Should().NotContain(":File:");
    }

    [Test]
    public async Task ModuleOutputs_MultipleReads_ProduceSingleEdgeToModule()
    {
        // Reading several outputs of the same module from one resource yields one DEPENDS_ON edge
        // from the resource to the module declaration.
        var result = await ParseAndEnrichAsync(("main.tf", """
            module "network" {
              source = "./modules/network"
            }

            resource "aws_instance" "app" {
              subnet_id = module.network.subnet_id
              vpc_id    = module.network.vpc_id
            }
            """));

        result.Relationships
            .Where(r => r.Kind == RelationshipKind.DependsOn && r.To.Contains(":Type:network:"))
            .Should().ContainSingle()
            .Which.From.Should().Contain(":Type:aws_instance.app");
    }

    [Test]
    public async Task SnsTopic_MapsToTopicNode_WorkspaceGlobalId()
    {
        // An SNS topic with a literal name becomes a Topic node named after the deployed topic,
        // with a workspace-global id so app code and the IaC reconcile.
        var result = await ParseAndEnrichAsync(("sns.tf", """
            resource "aws_sns_topic" "events" {
              name = "team-events"
            }
            """));

        var topic = result.SemanticNodes.OfType<TopicNode>().Should()
            .ContainSingle(t => t.Name == "team-events").Subject;
        topic.Id.Should().EndWith(":top:sns:team-events");
        topic.BrokerType.Should().Be(BrokerType.Sns);
    }

    [Test]
    public async Task CloudServiceId_IsScopedByDirectory()
    {
        // The CloudService id carries a directory scope segment so same-named plumbing resources
        // in different modules stay distinct; the node is named after the deployed resource and
        // typed by service and cloud provider.
        var result = await ParseAndEnrichAsync(("iam.tf", """
            resource "aws_iam_role" "exec" {
              name = "exec-role"
            }
            """));

        var svc = result.SemanticNodes.OfType<CloudServiceNode>().Should()
            .ContainSingle(s => s.Name == "exec-role").Subject;

        // Flat fixture → "root" scope segment between the resource type and the name.
        svc.Id.Should().Contain(":cloud:aws_iam_role:root:exec");
        svc.ResourceType.Should().Be(CloudResourceType.Iam);
        svc.Provider.Should().Be("aws");
    }

    [Test]
    public async Task LockFile_ProducesNoDeclarations()
    {
        // A .terraform.lock.hcl must not produce provider declarations named after registry URLs.
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
    public async Task Tfvars_AssignmentDependsOnVariable_SameDir()
    {
        // A .tfvars assignment is a member node carrying the assigned value, with a DEPENDS_ON
        // edge to the variable of the same name declared in the same directory.
        var result = await ParseAndEnrichAsync(
            ("variables.tf", """
                variable "env" { type = string }
                """),
            ("prod.tfvars", """
                env = "prod"
                """));

        var edge = result.Relationships.Should()
            .ContainSingle(r => r.Kind == RelationshipKind.DependsOn && r.Via == "tfvars")
            .Subject;

        edge.From.Should().Contain("prod.tfvars:Member:env:1");
        edge.To.Should().Contain("variables.tf:Member:env");
        edge.Line.Should().Be(1);
        result.Relationships.Should().NotContain(r => r.Kind == RelationshipKind.Configures);
    }

    [Test]
    public async Task NoEdge_StartsAtAFileNode()
    {
        // References in locals, top-level attributes, and blocks without a declaration start at a
        // member node (the local, or an attribute anchor), never at the file.
        var result = await ParseAndEnrichAsync(("terragrunt.hcl", """
            dependency "vpc" {
              config_path = "../vpc"
            }

            locals {
              base   = "../modules"
              region = "us-east-1"
              name   = "${local.region}-app"
            }

            terraform {
              source = "${local.base}/app"
            }

            inputs = {
              vpc_id = dependency.vpc.outputs.vpc_id
            }
            """));

        var dependsOn = result.Relationships.Where(r => r.Kind == RelationshipKind.DependsOn).ToList();
        dependsOn.Select(r => (Source: r.From[(r.From.IndexOf(":Member:") + 8)..], Target: r.To[(r.To.IndexOf(":sym:") + 5)..]))
            .Should().BeEquivalentTo(new[]
            {
                (Source: "name:8", Target: "terragrunt.hcl:Member:region:7"),
                (Source: "terraform.source:12", Target: "terragrunt.hcl:Member:base:6"),
                (Source: "inputs:15", Target: "terragrunt.hcl:Type:vpc:1")
            });
        dependsOn.Should().NotContain(r => r.From.Contains(":f:"));

        var anchors = Flatten(result.FileNode).OfType<MemberNode>()
            .Where(m => m.MemberKind == MemberKind.Property).ToDictionary(m => m.Name);
        anchors.Keys.Should().BeEquivalentTo("inputs", "terraform.source");
        anchors["terraform.source"].InitializerExpression.Should().Be("\"${local.base}/app\"");
    }

    [Test]
    public async Task HyphenatedName_InInterpolation_HarvestsFullName_NoTruncation()
    {
        // A hyphenated reference inside a ${...} interpolation harvests the full name
        // (var.my-flag), not one truncated at the hyphen (var.my) — which would miss the edge or,
        // with the decoy variable "my" present, resolve to the wrong node.
        var result = await ParseAndEnrichAsync(("main.tf", """
            variable "my-flag" { type = bool }
            variable "my" { type = string }

            resource "aws_s3_bucket" "b" {
              bucket = "prefix-${var.my-flag}-suffix"
            }
            """));

        var refEdges = result.Relationships
            .Where(r => r.Kind == RelationshipKind.DependsOn)
            .ToList();

        // The edge resolves to variable "my-flag", not the truncated decoy "my".
        refEdges.Should().Contain(r => r.To.Contains(":Member:my-flag"));
        refEdges.Should().NotContain(r => r.To.Contains(":Member:my:"));
    }

    [Test]
    public async Task EscapedInterpolation_ProducesNoReferenceEdge()
    {
        // "$${var.foo}" is an escaped literal "${var.foo}", not an interpolation, so it produces
        // no reference edge even when variable "foo" is declared. A real "${var.foo}" does.
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
            .Where(r => r.Kind == RelationshipKind.DependsOn && r.To.Contains(":Member:foo"))
            .ToList();

        // Exactly one edge — from the live interpolation resource, not the escaped one.
        varFooEdges.Should().ContainSingle();
        varFooEdges[0].From.Should().Contain(":Type:aws_s3_bucket.live");
        varFooEdges.Should().NotContain(r => r.From.Contains(":Type:aws_s3_bucket.escaped"));
    }

    [Test]
    public async Task AppInfraReconciliation_TerraformDbUsesCanonicalId()
    {
        // Infrastructure reconciles with application code by shared node id: the Database node
        // emitted for a Terraform database must use the canonical scheme app/config parsers target
        // (ws:db:{dbType}:{name}), so the two merge.
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
                .Where(n => n.Kind == NodeKind.Database
                    && n.Properties.GetValueOrDefault("name")?.ToString() == "orders")
                .DistinctBy(n => n.Id)
                .ToList();
            dbNodes.Should().ContainSingle();

            // The canonical reconciliation id: {ws}:{Database-prefix}:{dbType}:{name}, no extra scope.
            var dbId = dbNodes[0].Id;
            dbId.Should().EndWith($":{OntologyConstants.IdPrefixes.Database}:relational:orders");

            // That node is deployed by the Terraform resource.
            client.UploadedRelationships.Should().Contain(r =>
                r.Kind == RelationshipKind.DeployedBy
                && r.From == dbId && r.To.Contains("aws_db_instance.orders"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task DependsOn_SetsDependsOnEvidence()
    {
        // A depends_on meta-argument yields a DEPENDS_ON edge marked with depends_on evidence,
        // distinct from an implicit reference.
        var result = await ParseAndEnrichAsync(("main.tf", """
            resource "aws_iam_role" "r" { name = "role" }

            resource "aws_instance" "app" {
              ami        = "ami-123"
              depends_on = [aws_iam_role.r]
            }
            """));

        var edge = result.Relationships.Should()
            .ContainSingle(r => r.Kind == RelationshipKind.DependsOn
                && r.To.Contains("aws_iam_role.r"))
            .Subject;
        edge.EvidenceKind.Should().Be("depends_on");
        edge.From.Should().Contain(":Type:aws_instance.app");
    }

    [Test]
    public async Task DependsOn_AndImplicitReference_ToSameTarget_ProduceOneExplicitEdge()
    {
        // An implicit reference and a depends_on entry to the same target collapse into one edge,
        // which keeps the depends_on evidence regardless of order.
        var result = await ParseAndEnrichAsync(("main.tf", """
            resource "aws_iam_role" "r" { name = "role" }

            resource "aws_instance" "app" {
              iam_instance_profile = aws_iam_role.r.name
              depends_on           = [aws_iam_role.r]
            }
            """));

        result.Relationships
            .Where(r => r.Kind == RelationshipKind.DependsOn && r.To.Contains("aws_iam_role.r"))
            .Should().ContainSingle()
            .Which.EvidenceKind.Should().Be("depends_on");
    }

    [Test]
    public async Task DataSourceReference_ResolvesToDataDeclaration()
    {
        // A data.<type>.<name> reference resolves to the data source declaration.
        var result = await ParseAndEnrichAsync(("main.tf", """
            data "aws_ami" "ubuntu" {
              most_recent = true
            }

            resource "aws_instance" "app" {
              ami = data.aws_ami.ubuntu.id
            }
            """));

        var edge = result.Relationships.Should()
            .ContainSingle(r => r.Kind == RelationshipKind.DependsOn
                && r.To.Contains("aws_ami.ubuntu"))
            .Subject;
        edge.EvidenceKind.Should().BeNull();
        edge.From.Should().Contain(":Type:aws_instance.app");

        Flatten(result.FileNode).OfType<TypeNode>()
            .Should().ContainSingle(t => t.Name == "aws_ami.ubuntu")
            .Which.TypeKind.Should().Be(TypeKind.DataSource);
    }

    [Test]
    public async Task Declarations_MapToTypedKinds_WithLiteralInitializers()
    {
        // Each HCL block maps to a typed node kind; a default or value's source text becomes the
        // member's initializer expression.
        var result = await ParseAndEnrichAsync(("main.tf", """
            variable "env" {
              default = "dev"
            }

            locals {
              region = "us-east-1"
            }

            module "network" {
              source = "./modules/network"
            }

            resource "aws_s3_bucket" "data" {
              bucket = "data"
            }

            output "label" {
              value = "fixed"
            }
            """));

        var nodes = Flatten(result.FileNode).ToList();
        var members = nodes.OfType<MemberNode>().ToDictionary(m => m.Name);
        members["env"].MemberKind.Should().Be(MemberKind.Parameter);
        members["env"].InitializerExpression.Should().Be("\"dev\"");
        members["region"].MemberKind.Should().Be(MemberKind.Variable);
        members["region"].InitializerExpression.Should().Be("\"us-east-1\"");
        members["label"].MemberKind.Should().Be(MemberKind.Output);
        members["label"].InitializerExpression.Should().Be("\"fixed\"");

        var types = nodes.OfType<TypeNode>().ToDictionary(t => t.Name);
        types["network"].TypeKind.Should().Be(TypeKind.Module);
        types["aws_s3_bucket.data"].TypeKind.Should().Be(TypeKind.Resource);
        nodes.Should().OnlyContain(n => !(n is CompositeNode) || ((CompositeNode)n).CustomProperties == null);
    }

    [Test]
    public async Task ParseDependencies_ResolvesTerragruntPathsFromTheAst()
    {
        // Terragrunt path expressions resolve to local project paths; registry sources become
        // external packages; commented-out blocks and calls produce nothing.
        var root = Path.Combine(Path.GetTempPath(), "ce-tf-deps-" + Guid.NewGuid().ToString("N"));
        var unit = Path.Combine(root, "live", "app");
        Directory.CreateDirectory(unit);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "common.hcl"), "locals {}");
            await File.WriteAllTextAsync(Path.Combine(unit, "terragrunt.hcl"), """
                terraform {
                  source = "${get_terragrunt_dir()}/../../modules/app"
                }

                dependency "vpc" {
                  config_path = "../vpc"
                }

                dependencies {
                  paths = ["../iam", "../kms"]
                }

                locals {
                  common = read_terragrunt_config(find_in_parent_folders("common.hcl"))
                  label  = "${read_terragrunt_config(find_in_parent_folders("common.hcl")).locals.x}"
                }

                # dependency "old" { config_path = "../old" }
                // x = read_terragrunt_config("../commented/c.hcl")
                """);
            await File.WriteAllTextAsync(Path.Combine(unit, "main.tf"), """
                module "s3" {
                  source = "terraform-aws-modules/s3-bucket/aws"
                }
                """);

            var info = await new TerraformParser().ParseDependenciesAsync(unit);

            var rootDir = Path.GetFullPath(root).Replace('\\', '/');
            info.LocalProjectPaths.Should().BeEquivalentTo(
                $"{unit}/../../modules/app", "../vpc", "../iam", "../kms", rootDir);
            info.ExternalPackages.Select(p => p.Name).Should().Equal("terraform-aws-modules/s3-bucket/aws");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task MalformedBlock_DoesNotLoseOtherDeclarations()
    {
        // A syntax error inside one block costs only that attribute; the file's other
        // declarations and references are still produced.
        var result = await ParseAndEnrichAsync(("main.tf", """
            variable "env" { type = string }

            resource "aws_s3_bucket" "a" {
              bucket = foo(]
            }

            resource "aws_sqs_queue" "q" {
              name = var.env
            }
            """));

        Flatten(result.FileNode).OfType<TypeNode>().Select(t => t.Name)
            .Should().BeEquivalentTo("aws_s3_bucket.a", "aws_sqs_queue.q");
        result.Relationships.Should().Contain(r => r.Kind == RelationshipKind.DependsOn
            && r.From.Contains(":Type:aws_sqs_queue.q") && r.To.Contains(":Member:env"));
    }

    [Test]
    public async Task ProvisionedResources_AreDeployedByResourceAndProject_WithoutRuntimeEdges()
    {
        // Each provisioned node links back to its resource block and to the project; the project
        // gets no runtime-shaped edges (USES_DB / PUBLISHES_TO / USES_CLOUD), since provisioning
        // infrastructure is not using it.
        var result = await ParseAndEnrichInDirAsync("infra",
            ("main.tf", """
                resource "aws_db_instance" "orders" {
                  db_name = "orders"
                  engine  = "aurora-postgresql"
                }

                resource "aws_sqs_queue" "jobs" {
                  name = "jobs-queue"
                }

                resource "aws_iam_role" "worker" {
                  name = "worker-role"
                }
                """));

        result.Relationships.Should().NotContain(r =>
            r.Kind == RelationshipKind.UsesDb || r.Kind == RelationshipKind.PublishesTo
            || r.Kind == RelationshipKind.UsesCloud || r.Kind == RelationshipKind.Deploys);

        var deployedBy = result.Relationships.Where(r => r.Kind == RelationshipKind.DeployedBy).ToList();
        deployedBy.Should().HaveCount(6);
        foreach (var node in result.SemanticNodes)
        {
            deployedBy.Where(r => r.From == node.Id).Select(r => r.To).Should().HaveCount(2)
                .And.Contain(to => to.Contains(":sym:infra/main.tf:Type:"))
                .And.Contain(to => to.EndsWith(":p:infra:"));
        }

        var db = result.SemanticNodes.OfType<DatabaseNode>().Should().ContainSingle().Subject;
        db.Id.Should().EndWith(":db:relational:orders");
        db.Engine.Should().Be("postgresql");
        result.SemanticNodes.OfType<TopicNode>().Single().Id.Should().EndWith(":top:sqs:jobs-queue");
    }

    [Test]
    public async Task ComputedName_ScopesIdByModuleDirectory()
    {
        // A computed name cannot be reconciled with app code, so the node is named and identified
        // by module directory and label: the same module deployed from two directories yields two
        // distinct tables rather than one shared node.
        const string module = """
            resource "aws_dynamodb_table" "submissions" {
              name = "${var.app_name}-submissions"
            }
            """;

        var first = await ParseAndEnrichInDirAsync("units/a/extensions", ("tables.tf", module));
        var second = await ParseAndEnrichInDirAsync("units/b/extensions", ("tables.tf", module));

        var a = first.SemanticNodes.OfType<DatabaseNode>().Should().ContainSingle().Subject;
        var b = second.SemanticNodes.OfType<DatabaseNode>().Should().ContainSingle().Subject;
        a.Name.Should().Be("units/a/extensions/submissions");
        a.Id.Should().EndWith(":db:keyvalue:units/a/extensions/submissions");
        b.Id.Should().NotBe(a.Id);
        a.Engine.Should().Be("dynamodb");
    }

    [Test]
    public async Task SyntaxErrors_AreReportedAsDocumentDiagnostics()
    {
        // A syntax error does not fail the file: its declarations are still produced, and the error
        // is carried on the document for Layer 3 to record as a syntax-error scan issue.
        var dir = Path.Combine(Path.GetTempPath(), "ce-tf-diag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "main.tf");
            await File.WriteAllTextAsync(path, """
                resource "aws_s3_bucket" "a" {
                  bucket = foo(]
                }

                resource "aws_s3_bucket" "b" {}
                """);

            using var tree = await new TerraformParser().ParseAsync(path, "parent", "ws", dir);

            tree.Document.Should().NotBeNull();
            var diagnostic = tree.Document!.Diagnostics.Should().ContainSingle().Subject;
            diagnostic.Kind.Should().Be(SyntaxDiagnosticKind.SyntaxError);
            diagnostic.Span.StartLine.Should().Be(1); // 0-based: line 2 of the file
            Flatten(tree.FileNode).OfType<TypeNode>().Select(t => t.Name)
                .Should().BeEquivalentTo("aws_s3_bucket.a", "aws_s3_bucket.b");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
