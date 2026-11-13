using CodeExplorer.Parser.Terraform.Hcl;

namespace CodeExplorer.Parser.Terraform;

public enum TerraformSymbolKind : byte
{
    Resource,
    DataSource,
    Module,
    Provider,
    Dependency,
    Variable,
    Local,
    Output
}

/// <summary>
/// Represents a single Terraform/OpenTofu/Terragrunt declaration inside an HCL file
/// and the outbound references it makes.
/// </summary>
public sealed record TerraformSymbol(
    TerraformSymbolKind Kind,
    string CanonicalName,
    string? BlockClass,
    string ShortName,
    string NodeId,
    string FileNodeId,
    string RelativeFilePath,
    string ModuleDir,
    int StartLine,
    int EndLine,
    IReadOnlyList<HclReference> References,
    IReadOnlyDictionary<string, IReadOnlyList<HclReference>>? ModuleInputs = null,
    string? ModuleSourceRaw = null);
