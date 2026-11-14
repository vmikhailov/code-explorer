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
            topics.Should().ContainSingle(t => t.Id.EndsWith(":order-events", StringComparison.Ordinal));

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

    [Test]
    public void HclParser_HandlesSingleCharComparisonOperatorsUnaryNotAndDivision()
    {
        const string hcl = """
            variable "replicas" {
              type    = number
              default = 2
            }

            variable "disabled" {
              type    = bool
              default = false
            }

            locals {
              is_scaled  = var.replicas > 0 && var.replicas < 10
              is_enabled = !var.disabled
              half_count = var.replicas / 2
            }

            resource "aws_db_instance" "primary" {
              count         = local.is_scaled && local.is_enabled ? local.half_count : 0
              engine        = "postgres"
              instance_class = "db.t4g.micro"
            }
            """;

        var parsed = HclParser.Parse(hcl, "main.tf");
        parsed.Blocks.Should().HaveCount(4);
        parsed.Diagnostics.Should().BeNullOrEmpty();

        var locals = parsed.Blocks.Single(b => b.BlockHeader == "locals");
        var scaledRefs = locals.FindAttribute("is_scaled")!.Value.CollectReferences("is_scaled");
        scaledRefs.Should().Contain(r => r.CanonicalTarget == "var.replicas");

        var enabledRefs = locals.FindAttribute("is_enabled")!.Value.CollectReferences("is_enabled");
        enabledRefs.Should().ContainSingle(r => r.CanonicalTarget == "var.disabled");

        var halfRefs = locals.FindAttribute("half_count")!.Value.CollectReferences("half_count");
        halfRefs.Should().ContainSingle(r => r.CanonicalTarget == "var.replicas");
    }

    [Test]
    public void HclParser_IgnoresVarTextInsideStringLiteralsInObjectsAndTuples()
    {
        const string hcl = """
            variable "real_var" {
              type = string
            }

            locals {
              metadata = {
                description = "See var.fake_var and local.fake_local in docs"
                notes       = ["aws_s3_bucket.fake_bucket.arn", "module.fake_mod.out"]
                actual      = var.real_var
              }
            }
            """;

        var parsed = HclParser.Parse(hcl, "main.tf");
        var locals = parsed.Blocks.Single(b => b.BlockHeader == "locals");
        var refs = locals.FindAttribute("metadata")!.Value.CollectReferences("metadata");

        refs.Select(r => r.CanonicalTarget).Should().BeEquivalentTo(["var.real_var"]);
    }

    [Test]
    public void HclParser_HandlesTemplateDirectivesForComprehensionUnderscoreVarsAndMultiSegmentFunctions()
    {
        const string hcl = """
            variable "enable_logging" {
              type = bool
            }

            variable "subnet_items" {
              type = list(object({ id = string }))
            }

            variable "role_arn" {
              type = string
            }

            locals {
              subnet_ids = [for subnet_item in var.subnet_items : subnet_item.id]
              parsed_arn = provider::aws::arn_parse(var.role_arn)
              config_doc = "%{ if var.enable_logging ~}logging=true%{ else ~}logging=false%{ endif ~}"
              loop_doc   = "%{ for item_val in var.subnet_items }${item_val.id}%{ endfor }"
            }
            """;

        var parsed = HclParser.Parse(hcl, "main.tf");
        parsed.Diagnostics.Should().BeNullOrEmpty();

        var locals = parsed.Blocks.Single(b => b.BlockHeader == "locals");

        var subnetRefs = locals.FindAttribute("subnet_ids")!.Value.CollectReferences("subnet_ids");
        subnetRefs.Select(r => r.CanonicalTarget).Should().BeEquivalentTo(["var.subnet_items"]);
        subnetRefs.Should().NotContain(r => r.CanonicalTarget.StartsWith("subnet_item", StringComparison.Ordinal));

        var arnRefs = locals.FindAttribute("parsed_arn")!.Value.CollectReferences("parsed_arn");
        arnRefs.Select(r => r.CanonicalTarget).Should().BeEquivalentTo(["var.role_arn"]);

        var directiveRefs = locals.FindAttribute("config_doc")!.Value.CollectReferences("config_doc");
        directiveRefs.Select(r => r.CanonicalTarget).Should().BeEquivalentTo(["var.enable_logging"]);

        var loopRefs = locals.FindAttribute("loop_doc")!.Value.CollectReferences("loop_doc");
        loopRefs.Select(r => r.CanonicalTarget).Should().BeEquivalentTo(["var.subnet_items"]);
        loopRefs.Should().NotContain(r => r.CanonicalTarget.StartsWith("item_val", StringComparison.Ordinal));
    }

    [Test]
    public async Task HclParser_ErrorRecovery_RecordsDiagnosticsOnInvalidSyntaxAndContinuesParsing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ce-tf-err-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var tfFile = Path.Combine(tempRoot, "main.tf");
            await File.WriteAllTextAsync(tfFile, """
                resource "aws_s3_bucket" "first" {
                  bucket = "first-bucket"
                }

                'stray_single_quote_syntax_error

                resource "aws_s3_bucket" "second" {
                  bucket = "second-bucket"
                }
                """);

            var parser = new TerraformFileParser();
            var tree = await parser.ParseAsync(tfFile, "ws:proj:.", "ws", tempRoot);

            tree.Document.Should().NotBeNull();
            tree.Document!.Diagnostics.Should().NotBeEmpty();
            var resources = tree.FileNode.Children.OfType<CloudResourceNode>().ToList();
            resources.Select(r => r.Name).Should().BeEquivalentTo(
                ["aws_s3_bucket.first", "aws_s3_bucket.second"]);
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
    public async Task SemanticEnricher_WiresTfvarsAssignmentsTerragruntInputsAndConfigBlockReferences()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ce-tf-vars-" + Guid.NewGuid().ToString("N"));
        var modDir = Path.Combine(tempRoot, "modules", "service");
        var tgDir = Path.Combine(tempRoot, "live", "prod");
        Directory.CreateDirectory(modDir);
        Directory.CreateDirectory(tgDir);

        try
        {
            var modTf = Path.Combine(modDir, "main.tf");
            await File.WriteAllTextAsync(modTf, """
                variable "vpc_id" {
                  type = string
                }

                variable "instance_count" {
                  type = number
                }

                resource "aws_instance" "app" {
                  count     = var.instance_count
                  subnet_id = var.vpc_id
                }
                """);

            var tfvarsFile = Path.Combine(modDir, "prod.tfvars");
            await File.WriteAllTextAsync(tfvarsFile, """
                instance_count = 3
                """);

            var tgFile = Path.Combine(tgDir, "terragrunt.hcl");
            await File.WriteAllTextAsync(tgFile, """
                locals {
                  base_source = "../../modules/service"
                  custom_vpc  = "vpc-12345"
                }

                terraform {
                  source = local.base_source
                }

                dependency "vpc" {
                  config_path = "../vpc"
                }

                inputs = {
                  vpc_id         = dependency.vpc.outputs.vpc_id
                  instance_count = 4
                }
                """);

            var fileParser = new TerraformFileParser();
            var projectParser = new TerraformProjectParser();

            var modProject = new ProjectNode("ws:proj:modules/service", "service", "modules/service", ProjectLanguage.Terraform);
            var tgProject = new ProjectNode("ws:proj:live/prod", "prod", "live/prod", ProjectLanguage.Terraform);

            var modTree = await fileParser.ParseAsync(modTf, modProject.Id, "ws", tempRoot);
            var tfvarsTree = await fileParser.ParseAsync(tfvarsFile, modProject.Id, "ws", tempRoot);
            var tgTree = await fileParser.ParseAsync(tgFile, tgProject.Id, "ws", tempRoot);

            var allProjects = new List<ProjectNode> { modProject, tgProject };
            var treesByProject = new Dictionary<string, List<SyntaxTree>>
            {
                [modProject.Id] = [modTree, tfvarsTree],
                [tgProject.Id] = [tgTree]
            };
            var allFiles = new List<FileNode> { modTree.FileNode, tfvarsTree.FileNode, tgTree.FileNode };

            var modEnrichment = await projectParser.EnrichProjectAsync(new ProjectEnrichmentContext(
                "ws", tempRoot, modProject, [modTree, tfvarsTree], allProjects, treesByProject, allFiles));
            var tgEnrichment = await projectParser.EnrichProjectAsync(new ProjectEnrichmentContext(
                "ws", tempRoot, tgProject, [tgTree], allProjects, treesByProject, allFiles));

            // 1. prod.tfvars -> var.instance_count via = "tfvars"
            var tfvarsMember = tfvarsTree.FileNode.Children.OfType<MemberNode>().Single(m => m.Name == "tfvars.instance_count");
            var instanceCountVar = modTree.FileNode.Children.OfType<MemberNode>().Single(m => m.Name == "var.instance_count");
            modEnrichment.GlobalRelationships.Should().Contain(r =>
                r.From == tfvarsMember.Id &&
                r.To == instanceCountVar.Id &&
                r.Kind == RelationshipKind.DependsOn);

            // 2. terragrunt.hcl: terraform block -> local.base_source and inputs -> dependency.vpc
            var tfBlockMember = tgTree.FileNode.Children.OfType<MemberNode>().Single(m => m.Name == "terraform");
            var baseSourceLocal = tgTree.FileNode.Children.OfType<MemberNode>().Single(m => m.Name == "local.base_source");
            var inputsMember = tgTree.FileNode.Children.OfType<MemberNode>().Single(m => m.Name == "inputs");
            var depVpc = tgTree.FileNode.Children.OfType<TypeNode>().Single(t => t.Name == "dependency.vpc");

            tgEnrichment.GlobalRelationships.Should().Contain(r =>
                r.From == tfBlockMember.Id &&
                r.To == baseSourceLocal.Id &&
                r.Kind == RelationshipKind.DependsOn);

            tgEnrichment.GlobalRelationships.Should().Contain(r =>
                r.From == inputsMember.Id &&
                r.To == depVpc.Id &&
                r.Kind == RelationshipKind.DependsOn);
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
    public async Task WorkspaceIndexer_MixedAppAndTerraform_ReconcilesDatabasesWithoutFalseInfraUsesDbEdges()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ce-tf-mixed-" + Guid.NewGuid().ToString("N"));
        var apiDir = Path.Combine(tempRoot, "src", "Orders.Api");
        var infraDir = Path.Combine(tempRoot, "infra", "prod");
        var rdsModDir = Path.Combine(tempRoot, "infra", "modules", "rds");
        Directory.CreateDirectory(apiDir);
        Directory.CreateDirectory(infraDir);
        Directory.CreateDirectory(rdsModDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(apiDir, "Orders.Api.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
                  </ItemGroup>
                </Project>
                """);

            await File.WriteAllTextAsync(Path.Combine(apiDir, "appsettings.json"), """
                {
                  "ConnectionStrings": {
                    "OrdersDb": "Host=localhost;Database=orders;Username=postgres;Password=postgres"
                  }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(apiDir, "Program.cs"), """
                using Microsoft.EntityFrameworkCore;
                public class OrdersDbContext : DbContext
                {
                    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                        => optionsBuilder.UseNpgsql("Host=localhost;Database=orders");
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(infraDir, "main.tf"), """
                resource "aws_db_instance" "this" {
                  engine  = "postgres"
                  db_name = "orders"
                }

                module "aux_rds" {
                  source = "../modules/rds"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(rdsModDir, "main.tf"), """
                variable "db_identifier" {
                  type    = string
                  default = "aux"
                }

                resource "aws_db_instance" "this" {
                  engine     = "postgres"
                  identifier = var.db_identifier
                }
                """);

            using var scanResult = await WorkspaceIndexer.ScanInMemoryAsync(
                tempRoot,
                new WorkspaceScanOptions { MaxLayer = AnalysisLayer.Layer5_Global });

            scanResult.Graph.Should().NotBeNull();
            var nodes = scanResult.Graph!.UploadedNodes;
            var rels = scanResult.Graph.UploadedRelationships;

            // Nested Terraform modules (infra/prod and infra/modules/rds) + C# project are detected as separate projects
            var projects = nodes.Where(n => n.Kind == NodeKind.Project).ToList();
            projects.Should().HaveCountGreaterThanOrEqualTo(3);

            // Two distinct Database nodes: reconciled PostgreSQL.orders and distinct module-scoped infra/modules/rds/this
            var dbs = nodes.Where(n => n.Kind == NodeKind.Database).ToList();
            dbs.Should().Contain(d => d.Id.EndsWith(":orders", StringComparison.OrdinalIgnoreCase));
            dbs.Should().Contain(d => d.Id.Contains("rds_this", StringComparison.OrdinalIgnoreCase));

            var ordersDb = dbs.First(d => d.Id.EndsWith(":orders", StringComparison.OrdinalIgnoreCase));

            // CloudResource deploys ordersDb, and Orders.Api service uses ordersDb
            rels.Should().Contain(r => r.Kind == RelationshipKind.Deploys && r.To == ordersDb.Id);
            rels.Should().Contain(r => r.Kind == RelationshipKind.ImplementedBy && r.From == ordersDb.Id);
            rels.Should().Contain(r => r.Kind == RelationshipKind.UsesDb && r.To == ordersDb.Id);

            // Terraform infra projects/services must NOT have false runtime USES_DB edges
            var tfProjectIds = projects
                .Where(p => p.Id.Contains("infra", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            rels.Where(r => r.Kind == RelationshipKind.UsesDb)
                .Should().NotContain(r =>
                    tfProjectIds.Contains(r.From) ||
                    r.From.Contains(":s:prod", StringComparison.OrdinalIgnoreCase) ||
                    r.From.Contains(":s:rds", StringComparison.OrdinalIgnoreCase));
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
    [Category("GitHubCorpus")]
    public async Task GitHubCorpus_PublicTerraformAndTerragruntRepositories_ScansCleanlyAndExtractsOntology()
    {
        var repositories = new (string RepoSlug, string GitUrl, int MinProjects, int MinCloudResources)[]
        {
            ("terraform-aws-s3-bucket", "https://github.com/terraform-aws-modules/terraform-aws-s3-bucket.git", 2, 10),
            ("terraform-aws-vpc", "https://github.com/terraform-aws-modules/terraform-aws-vpc.git", 2, 20),
            ("terraform-aws-rds", "https://github.com/terraform-aws-modules/terraform-aws-rds.git", 4, 10),
            ("terraform-aws-rds-aurora", "https://github.com/terraform-aws-modules/terraform-aws-rds-aurora.git", 2, 10),
            ("terraform-aws-dynamodb-table", "https://github.com/terraform-aws-modules/terraform-aws-dynamodb-table.git", 2, 3),
            ("terraform-aws-sqs", "https://github.com/terraform-aws-modules/terraform-aws-sqs.git", 2, 4),
            ("terraform-aws-sns", "https://github.com/terraform-aws-modules/terraform-aws-sns.git", 2, 3),
            ("terraform-aws-lambda", "https://github.com/terraform-aws-modules/terraform-aws-lambda.git", 4, 15),
            ("terragrunt-infrastructure-live-example", "https://github.com/gruntwork-io/terragrunt-infrastructure-live-example.git", 5, 0)
        };

        var corpusRoot = Path.Combine(Path.GetTempPath(), "ce-tf-github-corpus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(corpusRoot);

        try
        {
            foreach (var (repoSlug, gitUrl, minProjects, minCloudResources) in repositories)
            {
                var repoDir = Path.Combine(corpusRoot, repoSlug);
                await CloneGitHubRepositoryShallowAsync(gitUrl, repoDir);

                using var scanResult = await WorkspaceIndexer.ScanInMemoryAsync(
                    repoDir,
                    new WorkspaceScanOptions { MaxLayer = AnalysisLayer.Layer5_Global });

                scanResult.Graph.Should().NotBeNull($"repository {repoSlug} should produce an indexed graph");
                var nodes = scanResult.Graph!.UploadedNodes;
                var rels = scanResult.Graph.UploadedRelationships;

                var projects = nodes.Where(n => n.Kind == NodeKind.Project).ToList();
                var files = nodes.Where(n => n.Kind == NodeKind.File).ToList();
                var cloudResources = nodes.Where(n => n.Kind == NodeKind.CloudResource).ToList();
                var clouds = nodes.Where(n => n.Kind == NodeKind.Cloud).ToList();

                projects.Count.Should().BeGreaterThanOrEqualTo(
                    minProjects,
                    $"repository {repoSlug} should discover nested Terraform/Terragrunt modules as projects");
                files.Should().NotBeEmpty($"repository {repoSlug} should index HCL/Terraform files");
                cloudResources.Count.Should().BeGreaterThanOrEqualTo(
                    minCloudResources,
                    $"repository {repoSlug} should extract expected CloudResource nodes");

                if (minCloudResources > 0)
                {
                    clouds.Should().NotBeEmpty($"repository {repoSlug} should synthesize Cloud provider nodes");
                    rels.Should().Contain(
                        r => r.Kind == RelationshipKind.HostedIn,
                        $"repository {repoSlug} should link CloudResources to Cloud providers via HOSTED_IN");
                }

                if (repoSlug is "terraform-aws-rds" or "terraform-aws-rds-aurora")
                {
                    var databases = nodes.Where(n => n.Kind == NodeKind.Database).ToList();
                    databases.Count.Should().BeGreaterThanOrEqualTo(
                        2,
                        $"{repoSlug} should produce distinct Database nodes without collapsing");
                    rels.Should().Contain(r => r.Kind == RelationshipKind.Deploys);
                    rels.Should().Contain(r => r.Kind == RelationshipKind.ImplementedBy);
                }

                if (repoSlug == "terraform-aws-sqs")
                {
                    var topics = nodes.Where(n => n.Kind == NodeKind.Topic).ToList();
                    topics.Should().NotBeEmpty("terraform-aws-sqs should produce Topic nodes for SQS queues");
                }

                TestContext.Out.WriteLine(
                    $"[GitHubCorpus] {repoSlug}: projects={projects.Count}, files={files.Count}, " +
                    $"cloudResources={cloudResources.Count}, nodes={nodes.Count}, rels={rels.Count}");
            }
        }
        finally
        {
            TryDeleteDirectory(corpusRoot);
        }
    }

    private static async Task CloneGitHubRepositoryShallowAsync(string gitUrl, string targetDir)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("clone");
        psi.ArgumentList.Add("--depth");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("--quiet");
        psi.ArgumentList.Add(gitUrl);
        psi.ArgumentList.Add(targetDir);

        using var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process.");
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            var stderr = await proc.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"git clone failed for {gitUrl} (exit {proc.ExitCode}): {stderr}");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Ignore cleanup locks on Windows temp folders
        }
    }
}
