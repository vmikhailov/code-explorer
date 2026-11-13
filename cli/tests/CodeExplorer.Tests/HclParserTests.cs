using AwesomeAssertions;
using CodeExplorer.Parser.Terraform;
using CodeExplorer.Parser.Terraform.Hcl;
using NUnit.Framework;

namespace CodeExplorer.Tests;

/// <summary>
/// HCL grammar, error recovery, and reference collection over inline sources.
/// </summary>
[TestFixture]
public class HclParserTests
{
    private static HclFile Parse(string source)
    {
        var file = HclParser.Parse(source);
        return file;
    }

    // Dotted paths of every reference collected from the source.
    private static List<string> RefPaths(string source) =>
        TerraformReferenceCollector.Collect(Parse(source)).References.Select(r => string.Join('.', r.Path)).ToList();

    private static HclExpr ValueOf(string source, string attribute = "x") =>
        Parse(source).Attributes.Single(a => a.Name == attribute).Value;

    // ---- References -----------------------------------------------------------------------------

    [Test]
    public void StringLiteral_MentioningTraversal_IsNotAReference()
    {
        var refs = RefPaths("""
            resource "aws_s3_bucket" "b" {
              tags = { description = "see var.foo documentation" }
            }
            """);

        refs.Should().BeEmpty();
    }

    [Test]
    public void Interpolation_InsideObject_IsAReference()
    {
        var refs = RefPaths("""
            resource "aws_s3_bucket" "b" {
              tags = { description = "see ${var.bar} documentation" }
            }
            """);

        refs.Should().Equal("var.bar");
    }

    [Test]
    public void MultiLineDependsOn_EachEntryIsExplicit()
    {
        var refs = TerraformReferenceCollector.Collect(Parse("""
            resource "aws_instance" "app" {
              ami = "ami-123"
              depends_on = [
                aws_iam_role.r,
                aws_iam_role.q,
              ]
            }
            """)).References;

        refs.Select(r => (string.Join('.', r.Path), r.Explicit, r.EnclosingKey, r.Line))
            .Should().Equal(
                ("aws_iam_role.r", true, "aws_instance.app", 4),
                ("aws_iam_role.q", true, "aws_instance.app", 5));
    }

    [Test]
    public void NestedQuotesInsideInterpolation_ParseAsOneTemplate()
    {
        var refs = RefPaths("""x = "${lookup(var.m, "k}")}-suffix" """);

        refs.Should().Equal("var.m");
    }

    [Test]
    public void EscapedTemplateSequences_AreLiteralText()
    {
        var value = ValueOf("""x = "$${var.a} %%{ if var.b }" """);

        value.Should().BeOfType<HclTemplate>()
            .Which.LiteralValue.Should().Be("${var.a} %{ if var.b }");
        RefPaths("""x = "$${var.a}" """).Should().BeEmpty();
    }

    [Test]
    public void Heredoc_InterpolationsAreReferences_QuotesAreText()
    {
        var source = """
            locals {
              policy = <<-EOT
                {"Resource": "${aws_s3_bucket.b.arn}", "Note": "var.not_a_ref"}
                EOT
              after = var.next
            }
            """;

        var refs = TerraformReferenceCollector.Collect(Parse(source)).References;

        refs.Select(r => (string.Join('.', r.Path), r.Line))
            .Should().Equal(("aws_s3_bucket.b.arn", 3), ("var.next", 5));
        Parse(source).Diagnostics.Should().BeEmpty();
    }

    [Test]
    public void ForExpressions_IterationVariablesAreNotReferences()
    {
        var refs = RefPaths("""
            a = [for s in var.subnets : s.id if s.public]
            b = {for k, v in local.map : k => v.name...}
            c = [for s in var.list : s.arn if s.region == var.region]
            """);

        refs.Should().Equal("var.subnets", "local.map", "var.list", "var.region");
    }

    [Test]
    public void TemplateForDirective_BindsVariablesUntilEndFor()
    {
        var refs = RefPaths("""
            x = "%{ for u in var.users }${u.name},%{ endfor }${u.after}"
            """);

        refs.Should().Equal("var.users", "u.after");
    }

    [Test]
    public void Splats_AndIndexes_KeepAttributePath()
    {
        var refs = RefPaths("""
            a = aws_instance.web[*].id
            b = aws_instance.web.*.id
            c = module.net.subnets[0]
            d = local.by_env[var.env].name
            """);

        refs.Should().BeEquivalentTo(
            "aws_instance.web.id", "aws_instance.web.id", "module.net.subnets", "local.by_env.name", "var.env");
    }

    [Test]
    public void ObjectKeys_BareNamesAreNotReferences_ParenthesizedKeysAre()
    {
        var refs = RefPaths("""
            x = {
              name     = var.name
              (var.key) = 1
              "quoted" = 2
            }
            """);

        refs.Should().BeEquivalentTo("var.name", "var.key");
    }

    [Test]
    public void HyphenatedIdentifier_VersusSpacedSubtraction()
    {
        RefPaths("x = var.my-flag").Should().Equal("var.my-flag");

        var difference = ValueOf("x = var.a - var.b").Should().BeOfType<HclBinary>().Subject;
        difference.Operator.Should().Be("-");
    }

    // ---- Expressions ----------------------------------------------------------------------------

    [Test]
    public void BinaryOperators_FollowHclPrecedence()
    {
        var sum = ValueOf("x = 1 + 2 * 3").Should().BeOfType<HclBinary>().Subject;
        sum.Operator.Should().Be("+");
        sum.Right.Should().BeOfType<HclBinary>().Which.Operator.Should().Be("*");

        var or = ValueOf("x = a == 1 || b && c").Should().BeOfType<HclBinary>().Subject;
        or.Operator.Should().Be("||");
        or.Right.Should().BeOfType<HclBinary>().Which.Operator.Should().Be("&&");
    }

    [Test]
    public void Conditional_AndUnary()
    {
        var conditional = ValueOf("x = !var.enabled ? 0 : -1").Should().BeOfType<HclConditional>().Subject;
        conditional.Condition.Should().BeOfType<HclUnary>().Which.Operator.Should().Be("!");
        conditional.WhenFalse.Should().BeOfType<HclUnary>().Which.Operator.Should().Be("-");
    }

    [Test]
    public void FunctionCalls_IncludingProviderNamespacedAndExpansion()
    {
        var call = ValueOf("x = provider::aws::arn_parse(var.arn)").Should().BeOfType<HclCall>().Subject;
        call.Function.Should().Be("provider::aws::arn_parse");

        ValueOf("x = concat(local.lists...)").Should().BeOfType<HclCall>()
            .Which.Arguments.Should().ContainSingle();
    }

    [Test]
    public void Comments_AllStyles_AndDivisionIsNotAComment()
    {
        var file = Parse("""
            # shell comment
            // line comment
            /* block
               comment */
            x = 10 / 2 # trailing
            """);

        file.Diagnostics.Should().BeEmpty();
        file.Attributes.Single().Value.Should().BeOfType<HclBinary>().Which.Operator.Should().Be("/");
        file.Attributes.Single().ValueText.Should().Be("10 / 2");
    }

    [Test]
    public void StringEscapes_AreDecoded()
    {
        ValueOf("""x = "a\"b\\c\n\u00e9" """).Should().BeOfType<HclTemplate>()
            .Which.LiteralValue.Should().Be("a\"b\\c\né");
    }

    // ---- Blocks ---------------------------------------------------------------------------------

    [Test]
    public void TopLevelBlocks_AreClassified_NestedBlocksAreOther()
    {
        var file = Parse("""
            resource "aws_security_group" "sg" {
              ingress {
                from_port = 443
              }
              dynamic "egress" {
                for_each = var.rules
                content { to_port = egress.value }
              }
            }
            locals { a = 1 }
            check "health" {}
            """);

        file.Blocks.Select(b => b.Kind).Should().Equal(HclBlockKind.Resource, HclBlockKind.Locals, HclBlockKind.Other);
        var resource = file.Blocks[0];
        resource.Labels.Should().Equal("aws_security_group", "sg");
        resource.Blocks.Select(b => b.Kind).Should().OnlyContain(k => k == HclBlockKind.Other);
        resource.Blocks[1].Labels.Should().Equal("egress");
        resource.Blocks[1].Blocks.Single().Attributes.Single().Name.Should().Be("to_port");
    }

    // ---- Error recovery -------------------------------------------------------------------------

    [Test]
    public void MalformedAttribute_CostsOnlyThatAttribute()
    {
        var file = Parse("""
            resource "aws_s3_bucket" "a" {
              bucket = "a"
              broken = foo(]
              acl    = var.acl
            }

            resource "aws_s3_bucket" "b" {
              bucket = var.name
            }
            """);

        file.Diagnostics.Should().ContainSingle().Which.Line.Should().Be(3);
        file.Blocks.Should().HaveCount(2);
        file.Blocks[0].Attributes.Select(a => a.Name).Should().Equal("bucket", "acl");
        TerraformReferenceCollector.Collect(file).References.Select(r => string.Join('.', r.Path))
            .Should().Equal("var.acl", "var.name");
    }

    [Test]
    public void StrayCharacterBeforeBlock_DoesNotLoseTheBlock()
    {
        var file = Parse("""
            'include "root" {
              path = find_in_parent_folders()
            }
            """);

        file.Diagnostics.Should().ContainSingle().Which.Line.Should().Be(1);
        file.Blocks.Should().ContainSingle().Which.Kind.Should().Be(HclBlockKind.Include);
    }

    [Test]
    public void UnterminatedString_RecoversAtNextLine()
    {
        var file = Parse("""
            a = "unterminated
            b = var.ok
            """);

        file.Diagnostics.Should().ContainSingle().Which.Line.Should().Be(1);
        file.Attributes.Select(a => a.Name).Should().Equal("b");
    }

    [Test]
    public void UnclosedBlockAtEndOfFile_KeepsItsContents()
    {
        var file = Parse("""
            module "net" {
              source = "./net"
            """);

        file.Diagnostics.Should().ContainSingle();
        file.Blocks.Single().Attributes.Single().Name.Should().Be("source");
    }

    [Test]
    public void TrailingTokensAfterValue_KeepsTheAttribute()
    {
        var file = Parse("""
            a = 1 2
            b = 3
            """);

        file.Diagnostics.Should().ContainSingle().Which.Line.Should().Be(1);
        file.Attributes.Select(a => a.Name).Should().Equal("a", "b");
    }

    [Test]
    public void NonHclTemplateSyntax_ProducesDiagnostics_NotExceptions()
    {
        var file = Parse("""
            locals {
              enabled = {{feature_enabled}}
              name    = "{{name}}"
            }
            """);

        file.Diagnostics.Should().ContainSingle().Which.Line.Should().Be(2);
        file.Blocks.Single().Attributes.Single().Name.Should().Be("name");
    }

    // ---- Reference sources ----------------------------------------------------------------------

    [Test]
    public void EveryReference_HasASource_DeclarationLocalOrAnchor()
    {
        var collected = TerraformReferenceCollector.Collect(Parse("""
            locals {
              prefix = var.env
            }

            inputs = {
              vpc_id = dependency.vpc.outputs.vpc_id
              plain  = "no references"
            }

            terraform {
              source = "${local.base}/mod"
              extra_arguments "vars" {
                arguments = [var.file]
              }
            }

            generate "provider" {
              path     = "provider.tf"
              contents = <<EOF
            provider "aws" { region = "${local.region}" }
            EOF
            }

            resource "aws_s3_bucket" "b" {
              bucket = local.prefix
            }
            """));

        collected.References.Select(r => (string.Join('.', r.Path), r.EnclosingKey)).Should().Equal(
            ("var.env", "local.prefix"),
            ("dependency.vpc.outputs.vpc_id", "#inputs:5"),
            ("local.base", "#terraform.source:11"),
            ("var.file", "#terraform.extra_arguments.vars.arguments:13"),
            ("local.region", "#generate.provider.contents:19"),
            ("local.prefix", "aws_s3_bucket.b"));

        // Only attributes that make references are anchored (not `generate.provider.path`).
        collected.Anchors.Select(a => a.Name).Should().Equal(
            "inputs", "terraform.source", "terraform.extra_arguments.vars.arguments", "generate.provider.contents");
    }

    [Test]
    public void Tfvars_TopLevelAssignments_AreNotAnchors()
    {
        var collected = TerraformReferenceCollector.Collect(Parse("""env = "prod" """), isTfvars: true);

        collected.Anchors.Should().BeEmpty();
        collected.References.Should().BeEmpty();
    }
}
