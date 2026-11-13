using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parser.Terraform;
using CodeExplorer.Parser.Terraform.Hcl;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class TerraformParserTests
{
    [Test]
    public void HclParser_ParsesBlocksInterpolationsHeredocsAndForExpressions()
    {
        const string hcl = """
            # Root infrastructure configuration
            terraform {
              required_version = ">= 1.6.0"
              required_providers {
                aws = {
                  source  = "hashicorp/aws"
                  version = "~> 5.0"
                }
              }
            }

            variable "environment" {
              type    = string
              default = "prod"
            }

            locals {
              bucket_prefix = "corp-${var.environment}"
              subnet_ids    = [for s in aws_subnet.private : s.id]
            }

            resource "aws_s3_bucket" "assets" {
              bucket = "${local.bucket_prefix}-assets"
              policy = <<-EOF
                {
                  "Version": "2012-10-17",
                  "Resource": "arn:aws:s3:::${aws_iam_role.reader.name}/*"
                }
              EOF
              depends_on = [
                aws_iam_role.reader,
                module.network
              ]
            }

            output "bucket_arn" {
              value = aws_s3_bucket.assets.arn
            }
            """;

        var parsed = HclParser.Parse(hcl, "main.tf");

        parsed.Blocks.Should().HaveCount(5);

        var resourceBlock = parsed.Blocks.Single(b => b.BlockHeader == "resource");
        resourceBlock.Labels.Should().Equal(["aws_s3_bucket", "assets"]);

        var refs = resourceBlock.CollectReferences();
        refs.Should().Contain(r => r.CanonicalTarget == "local.bucket_prefix");
        refs.Should().Contain(r => r.CanonicalTarget == "aws_iam_role.reader" && r.Attribute == "name");
        refs.Should().Contain(r => r.CanonicalTarget == "aws_iam_role.reader" && r.Via == "depends_on");
        refs.Should().Contain(r => r.CanonicalTarget == "module.network" && r.Via == "depends_on");

        var localsBlock = parsed.Blocks.Single(b => b.BlockHeader == "locals");
        var subnetAttr = localsBlock.FindAttribute("subnet_ids");
        subnetAttr.Should().NotBeNull();
        var subnetRefs = subnetAttr!.Value.CollectReferences("subnet_ids");
        subnetRefs.Should().Contain(r => r.CanonicalTarget == "aws_subnet.private");
    }

    [Test]
    public void FileParser_CanParseFile_AcceptsTfTofuHclAndRejectsLockFiles()
    {
        var parser = new TerraformFileParser();

        parser.CanParse(".tf").Should().BeTrue();
        parser.CanParse(".tofu").Should().BeTrue();
        parser.CanParse(".tfvars").Should().BeTrue();
        parser.CanParse(".hcl").Should().BeFalse();

        parser.CanParseFile("infra/main.tf").Should().BeTrue();
        parser.CanParseFile("infra/main.tofu").Should().BeTrue();
        parser.CanParseFile("live/prod/terragrunt.hcl").Should().BeTrue();
        parser.CanParseFile("infra/.terraform.lock.hcl").Should().BeFalse();
        parser.CanParseFile("infra/.tflint.hcl").Should().BeFalse();
    }

    [Test]
    public async Task FileParser_EmitsCloudResourceNodesAndPreventsCrossModuleIdCollisions()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ce-tf-test-" + Guid.NewGuid().ToString("N"));
        var devDir = Path.Combine(tempRoot, "envs", "dev");
        var prodDir = Path.Combine(tempRoot, "envs", "prod");
        Directory.CreateDirectory(devDir);
        Directory.CreateDirectory(prodDir);

        try
        {
            const string tfContent = """
                provider "aws" {
                  region = "eu-central-1"
                }

                variable "db_instance_class" {
                  type    = string
                  default = "db.t4g.micro"
                }

                locals {
                  db_name = "orders"
                }

                resource "aws_db_instance" "orders" {
                  engine         = "postgres"
                  instance_class = var.db_instance_class
                  db_name        = local.db_name
                }

                data "aws_caller_identity" "current" {}

                module "vpc" {
                  source = "../../modules/vpc"
                }

                output "db_endpoint" {
                  value = aws_db_instance.orders.endpoint
                }
                """;

            var devFile = Path.Combine(devDir, "main.tf");
            var prodFile = Path.Combine(prodDir, "main.tf");
            await File.WriteAllTextAsync(devFile, tfContent);
            await File.WriteAllTextAsync(prodFile, tfContent);

            var parser = new TerraformFileParser();
            var devTree = await parser.ParseAsync(devFile, "ws:proj:envs/dev", "ws", tempRoot);
            var prodTree = await parser.ParseAsync(prodFile, "ws:proj:envs/prod", "ws", tempRoot);

            var devCloudResources = devTree.FileNode.Children.OfType<CloudResourceNode>().ToList();
            var prodCloudResources = prodTree.FileNode.Children.OfType<CloudResourceNode>().ToList();

            devCloudResources.Should().HaveCount(2);
            prodCloudResources.Should().HaveCount(2);

            var devDbResource = devCloudResources.Single(r => r.Name == "aws_db_instance.orders");
            var prodDbResource = prodCloudResources.Single(r => r.Name == "aws_db_instance.orders");

            devDbResource.Kind.Should().Be(NodeKind.CloudResource);
            devDbResource.ResourceType.Should().Be("aws_db_instance");
            devDbResource.Provider.Should().Be("aws");
            devDbResource.ResourceRole.Should().Be("database");
            devDbResource.Mode.Should().Be("managed");
            devDbResource.Id.Should().NotBe(prodDbResource.Id);

            var dataResource = devCloudResources.Single(r => r.Name == "data.aws_caller_identity.current");
            dataResource.Mode.Should().Be("data");
            dataResource.ResourceRole.Should().Be("identity");

            var cloudNode = devTree.FileNode.Children.OfType<CloudNode>().Single();
            cloudNode.Provider.Should().Be("aws");
            cloudNode.Region.Should().Be("eu-central-1");

            var members = devTree.FileNode.Children.OfType<MemberNode>().ToList();
            members.Should().Contain(m => m.Name == "var.db_instance_class" && m.MemberKind == MemberKind.Variable);
            members.Should().Contain(m => m.Name == "local.db_name" && m.MemberKind == MemberKind.Local);
            members.Should().Contain(m => m.Name == "output.db_endpoint" && m.MemberKind == MemberKind.Output);

            var moduleNode = devTree.FileNode.Children.OfType<TypeNode>().Single();
            moduleNode.Name.Should().Be("module.vpc");
            moduleNode.TypeKind.Should().Be(TypeKind.Module);

            devTree.RootNode!.FirstChildOfType<HclDocumentSyntaxNode>().Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task SemanticEnricher_WiresCloudResourcesLogicalDatabasesTopicsAndCrossModuleEdges()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ce-tf-enrich-" + Guid.NewGuid().ToString("N"));
        var dbModDir = Path.Combine(tempRoot, "modules", "db");
        var appEnvDir = Path.Combine(tempRoot, "live", "prod", "app");
        var tgDir = Path.Combine(tempRoot, "live", "prod", "terragrunt-app");
        Directory.CreateDirectory(dbModDir);
        Directory.CreateDirectory(appEnvDir);
        Directory.CreateDirectory(tgDir);

        try
        {
            var dbModTf = Path.Combine(dbModDir, "main.tf");
            await File.WriteAllTextAsync(dbModTf, """
                variable "allocated_storage" {
                  type    = number
                  default = 20
                }

                resource "aws_db_instance" "orders" {
                  engine            = "postgres"
                  allocated_storage = var.allocated_storage
                }

                output "connection_string" {
                  value = aws_db_instance.orders.endpoint
                }
                """);

            var appEnvTf = Path.Combine(appEnvDir, "main.tf");
            await File.WriteAllTextAsync(appEnvTf, """
                provider "aws" {
                  region = "us-east-1"
                }

                locals {
                  storage_gb = 100
                }

                module "orders_db" {
                  source            = "../../../modules/db"
                  allocated_storage = local.storage_gb
                }

                resource "aws_sqs_queue" "order_events" {
                  name = "order-events"
                }

                resource "aws_lambda_function" "order_worker" {
                  function_name = "order-worker"
                  environment {
                    variables = {
                      DB_ENDPOINT = module.orders_db.connection_string
                      QUEUE_URL   = aws_sqs_queue.order_events.id
                    }
                  }
                }
                """);

            var tgHcl = Path.Combine(tgDir, "terragrunt.hcl");
            await File.WriteAllTextAsync(tgHcl, """
                terraform {
                  source = "../../../modules/db"
                }

                dependency "app" {
                  config_path = "../app"
                }

                inputs = {
                  allocated_storage = 50
                }
                """);

            var fileParser = new TerraformFileParser();
            var projectParser = new TerraformProjectParser();

            var dbProject = new ProjectNode("ws:proj:modules/db", "db", "modules/db", ProjectLanguage.Terraform);
            var appProject = new ProjectNode("ws:proj:live/prod/app", "app", "live/prod/app", ProjectLanguage.Terraform);
            var tgProject = new ProjectNode("ws:proj:live/prod/terragrunt-app", "terragrunt-app", "live/prod/terragrunt-app", ProjectLanguage.Terraform);

            var dbTree = await fileParser.ParseAsync(dbModTf, dbProject.Id, "ws", tempRoot);
            var appTree = await fileParser.ParseAsync(appEnvTf, appProject.Id, "ws", tempRoot);
            var tgTree = await fileParser.ParseAsync(tgHcl, tgProject.Id, "ws", tempRoot);

            var allProjects = new List<ProjectNode> { dbProject, appProject, tgProject };
            var treesByProject = new Dictionary<string, List<SyntaxTree>>
            {
                [dbProject.Id] = [dbTree],
                [appProject.Id] = [appTree],
                [tgProject.Id] = [tgTree]
            };
            var allFiles = new List<FileNode>
            {
                dbTree.FileNode,
                appTree.FileNode,
                tgTree.FileNode
            };

            var dbEnrichment = await projectParser.EnrichProjectAsync(new ProjectEnrichmentContext(
                "ws", tempRoot, dbProject, [dbTree], allProjects, treesByProject, allFiles));

            var appEnrichment = await projectParser.EnrichProjectAsync(new ProjectEnrichmentContext(
                "ws", tempRoot, appProject, [appTree], allProjects, treesByProject, allFiles));

            var tgEnrichment = await projectParser.EnrichProjectAsync(new ProjectEnrichmentContext(
                "ws", tempRoot, tgProject, [tgTree], allProjects, treesByProject, allFiles));

            // 1. Database logical entity + IMPLEMENTED_BY / DEPLOYS / HOSTED_IN in db module
            var dbEntity = dbEnrichment.SemanticNodes.OfType<DatabaseNode>().Single();
            dbEntity.Engine.Should().Be("postgres");
            dbEntity.ResourceType.Should().Be("aws_db_instance");
            dbEntity.ProvisionedBy.Should().Be("terraform");

            var dbCloudResource = dbTree.FileNode.Children.OfType<CloudResourceNode>().Single();
            dbEnrichment.SemanticRelationships.Should().Contain(r =>
                r.From == dbEntity.Id &&
                r.To == dbCloudResource.Id &&
                r.Kind == RelationshipKind.ImplementedBy);
            dbEnrichment.SemanticRelationships.Should().Contain(r =>
                r.From == dbCloudResource.Id &&
                r.To == dbEntity.Id &&
                r.Kind == RelationshipKind.Deploys);

            // Synthesized fallback CloudNode when module has no explicit provider block
            var synthesizedCloud = dbEnrichment.SemanticNodes.OfType<CloudNode>().Single();
            synthesizedCloud.Provider.Should().Be("aws");
            dbEnrichment.SemanticRelationships.Should().Contain(r =>
                r.From == dbCloudResource.Id &&
                r.To == synthesizedCloud.Id &&
                r.Kind == RelationshipKind.HostedIn);

            // 2. Topic logical entity + cross-module wiring in app project
            var topicEntity = appEnrichment.SemanticNodes.OfType<TopicNode>().Single();
            topicEntity.ResourceType.Should().Be("aws_sqs_queue");
            var sqsCloudResource = appTree.FileNode.Children.OfType<CloudResourceNode>().Single(r => r.Name == "aws_sqs_queue.order_events");
            var lambdaCloudResource = appTree.FileNode.Children.OfType<CloudResourceNode>().Single(r => r.Name == "aws_lambda_function.order_worker");

            appEnrichment.SemanticRelationships.Should().Contain(r =>
                r.From == topicEntity.Id &&
                r.To == sqsCloudResource.Id &&
                r.Kind == RelationshipKind.ImplementedBy);

            // Lambda depends on SQS queue and on db module's output.connection_string
            appEnrichment.GlobalRelationships.Should().Contain(r =>
                r.From == lambdaCloudResource.Id &&
                r.To == sqsCloudResource.Id &&
                r.Kind == RelationshipKind.DependsOn);

            var dbOutputNode = dbTree.FileNode.Children.OfType<MemberNode>().Single(m => m.Name == "output.connection_string");
            var dbVarNode = dbTree.FileNode.Children.OfType<MemberNode>().Single(m => m.Name == "var.allocated_storage");
            var appModuleNode = appTree.FileNode.Children.OfType<TypeNode>().Single(t => t.Name == "module.orders_db");

            appEnrichment.GlobalRelationships.Should().Contain(r =>
                r.From == lambdaCloudResource.Id &&
                r.To == dbOutputNode.Id &&
                r.Kind == RelationshipKind.DependsOn);

            appEnrichment.GlobalRelationships.Should().Contain(r =>
                r.From == appModuleNode.Id &&
                r.To == dbVarNode.Id &&
                r.Kind == RelationshipKind.Configures);

            // 3. Terragrunt inputs wiring to target module variable
            tgEnrichment.GlobalRelationships.Should().Contain(r =>
                r.From == tgTree.FileNode.Id &&
                r.To == dbVarNode.Id &&
                r.Kind == RelationshipKind.Configures);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task ProjectParser_ExtractsLocalModulesRegistryModulesProvidersAndTerragruntDeps()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ce-tf-proj-" + Guid.NewGuid().ToString("N"));
        var vpcDir = Path.Combine(tempRoot, "modules", "vpc");
        var appDir = Path.Combine(tempRoot, "live", "prod");
        Directory.CreateDirectory(vpcDir);
        Directory.CreateDirectory(appDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(vpcDir, "main.tf"), """
                resource "aws_vpc" "main" {
                  cidr_block = "10.0.0.0/16"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(appDir, "main.tf"), """
                terraform {
                  required_providers {
                    aws = {
                      source  = "hashicorp/aws"
                      version = "~> 5.0"
                    }
                  }
                }

                module "vpc" {
                  source = "../../modules/vpc"
                }

                module "eks" {
                  source  = "terraform-aws-modules/eks/aws"
                  version = "20.8.4"
                }
                """);

            var projectParser = new TerraformProjectParser();
            projectParser.IsProjectDirectory(appDir, Directory.GetFiles(appDir)).Should().BeTrue();

            var deps = await projectParser.ParseDependenciesAsync(appDir);
            deps.LocalProjectPaths.Should().Contain(p => p.EndsWith("modules/vpc", StringComparison.OrdinalIgnoreCase));
            deps.ExternalPackages.Should().Contain(pkg => pkg.Name == "hashicorp/aws" && pkg.Version == "~> 5.0");
            deps.ExternalPackages.Should().Contain(pkg => pkg.Name == "terraform-aws-modules/eks/aws" && pkg.Version == "20.8.4");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Test]
    public async Task ScanInMemoryAsync_EndToEndTerraformWorkspace_PopulatesCloudResourceOntologyInGraph()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ce-tf-e2e-" + Guid.NewGuid().ToString("N"));
        var dbModDir = Path.Combine(tempRoot, "modules", "db");
        var appEnvDir = Path.Combine(tempRoot, "live", "prod");
        Directory.CreateDirectory(dbModDir);
        Directory.CreateDirectory(appEnvDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(dbModDir, "main.tf"), """
                variable "db_instance_class" {
                  type    = string
                  default = "db.t4g.small"
                }

                resource "aws_db_instance" "orders" {
                  engine         = "postgres"
                  instance_class = var.db_instance_class
                }

                output "endpoint" {
                  value = aws_db_instance.orders.endpoint
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(appEnvDir, "main.tf"), """
                provider "aws" {
                  region = "eu-west-1"
                }

                module "orders_db" {
                  source            = "../../modules/db"
                  db_instance_class = "db.r6g.large"
                }

                resource "aws_sqs_queue" "events" {
                  name = "order-events"
                }
                """);

            using var scanResult = await WorkspaceIndexer.ScanInMemoryAsync(
                tempRoot,
                new WorkspaceScanOptions { MaxLayer = AnalysisLayer.Layer5_Global });

            scanResult.Graph.Should().NotBeNull();

            var cloudResources = scanResult.Graph!.UploadedNodes
                .Where(n => n.Kind == NodeKind.CloudResource)
                .ToList();
            cloudResources.Should().HaveCount(2);
            cloudResources.Should().Contain(n => n.Id.EndsWith(":cr:aws_db_instance.orders", StringComparison.Ordinal));
            cloudResources.Should().Contain(n => n.Id.EndsWith(":cr:aws_sqs_queue.events", StringComparison.Ordinal));

            var databases = scanResult.Graph.UploadedNodes
                .Where(n => n.Kind == NodeKind.Database)
                .ToList();
            databases.Should().ContainSingle(d => d.Id.EndsWith(":orders", StringComparison.Ordinal));

            var topics = scanResult.Graph.UploadedNodes
                .Where(n => n.Kind == NodeKind.Topic)
                .ToList();
            topics.Should().ContainSingle(t => t.Id.EndsWith(":events", StringComparison.Ordinal));

            scanResult.Graph.UploadedRelationships.Should().Contain(r =>
                r.Kind == RelationshipKind.ImplementedBy &&
                r.From == databases[0].Id);
            scanResult.Graph.UploadedRelationships.Should().Contain(r =>
                r.Kind == RelationshipKind.ImplementedBy &&
                r.From == topics[0].Id);
            scanResult.Graph.UploadedRelationships.Should().Contain(r =>
                r.Kind == RelationshipKind.HostedIn &&
                cloudResources.Select(cr => cr.Id).Contains(r.From));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
