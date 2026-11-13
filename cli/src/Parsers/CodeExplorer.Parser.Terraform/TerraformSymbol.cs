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
    TerraformSymbolKind Kind,
    string RelativeDir,   // module directory (relative), used to scope resolution
    int Line,
    TerraformResourceType? ResourceType = null,  // for resource/data: the TF type, e.g. "aws_sqs_queue"
    string? Name = null,          // for resource/data: the TF name label, e.g. "airflow_event_trigger"
    string? DeployedName = null,  // for resources: the literal cloud resource name, e.g. name = "orders-queue"
    string? Engine = null);       // for database resources: the engine, e.g. "postgresql", "dynamodb"

/// <summary>The HCL block a <see cref="TerraformSymbol"/> was declared by.</summary>
public enum TerraformSymbolKind
{
    Resource,
    DataSource,
    Module,
    Provider,
    Variable,
    Local,
    Output,
    Dependency,

    /// <summary>
    /// An attribute anchor (<see cref="TerraformAnchor"/>): a source of references, never a target,
    /// and scoped to its own file.
    /// </summary>
    Attribute
}

/// <summary>
/// A provider-defined Terraform resource type name (e.g. <c>aws_sqs_queue</c>). Open-ended — each
/// provider defines its own — so it is carried as a typed name rather than an enum.
/// </summary>
public readonly record struct TerraformResourceType(string Name)
{
    public override string ToString() => Name;
}


/// <summary>
/// A <c>.tfvars</c> assignment, declared as a member node (<see cref="NodeId"/>). The Layer 4
/// enricher links it to the <c>variable</c> of the same name declared in the same module directory;
/// no edge is emitted when the variable is not declared there.
/// </summary>
public sealed record TerraformConfigure(
    string NodeId,
    string VariableName,     // the key being assigned, e.g. "env"
    int Line);
