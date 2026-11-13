namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// A declaration surfaced by <see cref="TerraformDeclarationMapper"/> during Layer 3,
/// indexed so the Layer 4 enricher can resolve traversal references to it.
///
/// <paramref name="RefKey"/> is the dotted key a reference uses to point at this declaration,
/// scoped within a module directory:
///   variable "env"                 -> "var.env"
///   locals  { region = }           -> "local.region"
///   module  "network"              -> "module.network"
///   output  "vpc_id"               -> "output.vpc_id"   (module.X.vpc_id resolves cross-module)
///   resource "aws_s3_bucket" "x"   -> "aws_s3_bucket.x"
///   data "aws_ami" "x"             -> "data.aws_ami.x"
/// </summary>
public sealed record TerraformSymbol(
    string NodeId,
    string RefKey,
    string Kind,          // resource | data_source | module | provider | variable | local | output | dependency
    string RelativeDir,   // module directory (relative), used to scope resolution
    int Line,
    string? ResourceType = null,  // for resource/data: the TF type, e.g. "aws_sqs_queue"
    string? Name = null);         // for resource/data: the TF name label, e.g. "airflow_event_trigger"


/// <summary>
/// A configuration intent surfaced during Layer 3: a value assigned to a Terraform
/// <c>variable</c>, which the Layer 4 enricher resolves to the target variable declaration and
/// emits as a <c>CONFIGURES</c> edge. Two sources:
///   - a <c>.tfvars</c> assignment (<paramref name="Via"/> = "tfvars"), target variable in the
///     same module directory (or cross-dir, marked inferred);
///   - a Terragrunt <c>inputs = { }</c> key (<paramref name="Via"/> = "terragrunt"), target
///     variable in the wrapped module resolved from this unit's <c>terraform { source }</c>.
/// When the target module is not in the scanned workspace (e.g. a remote git module), no edge is
/// emitted — the target variable node does not exist to point at.
/// </summary>
public sealed record TerraformConfigure(
    string VariableName,     // the key being assigned, e.g. "env"
    string Via,              // "tfvars" | "terragrunt"
    int Line,
    string? SourceDir = null);  // for inputs: resolved wrapped-module dir (relative); null => same-dir (tfvars)
