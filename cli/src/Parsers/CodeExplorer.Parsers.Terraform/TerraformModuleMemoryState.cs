using CodeExplorer.Parsers.Terraform.Hcl;

namespace CodeExplorer.Parsers.Terraform;

internal sealed class TerraformModuleMemoryState
{
    public required string ModuleDir { get; init; }
    public Dictionary<string, TerraformSymbol> SymbolsByCanonical { get; } = new(StringComparer.Ordinal);
    public List<TerraformSymbol> AllSymbols { get; } = [];
    public List<HclFile> HclFiles { get; } = [];
    public string? TerragruntFileNodeId { get; set; }
    public int TerragruntInputsLine { get; set; }
    public HclExpr? TerragruntSourceExpr { get; set; }
    public List<HclExpr> TerragruntIncludePathExprs { get; } = [];
    public Dictionary<string, IReadOnlyList<HclReference>> TerragruntInputs { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, HclExpr> DependencyConfigPathExprs { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, HclExpr> IncludePathExprsBySymbol { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> LocalStrings { get; } = new(StringComparer.Ordinal);
}
