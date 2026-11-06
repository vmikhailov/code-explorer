using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Parser.Terraform.Hcl;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Maps HCL blocks → ce Layer 3 nodes (TypeNode / MemberNode) with kind/resource_type
/// properties and workspace-scoped URN IDs.
///
/// URN: {workspaceId}:sym:{relativePath}:{Kind}:{name}:{line}  (prevents cross-module collisions)
///
/// Block → node:
///   resource "T" "N"  -> TypeNode   kind=resource     resource_type=T  name=N
///   data "T" "N"      -> TypeNode   kind=data_source  resource_type=T  name=N
///   module "N"        -> TypeNode   kind=module       source=...        name=N
///   provider "N"      -> TypeNode   kind=provider                       name=N
///   variable "N"      -> MemberNode kind=variable
///   locals { X=... }  -> MemberNode kind=local  (one per entry)
///   output "N"        -> MemberNode kind=output
///   terraform { }     -> no node (manifest props only)
///
/// Returns the declared <see cref="TerraformSymbol"/>s so the Layer 4 enricher can resolve
/// traversal references to them. Only emits TypeNode/MemberNode (never literal "Variable"/
/// "Member" kinds — BaseParserVisitor drops those — and never custom Terraform labels).
/// </summary>
public static class TerraformDeclarationMapper
{
    public static string Urn(string workspaceId, string relativePath, string kind, string name, int line)
        => $"{workspaceId}:sym:{relativePath}:{kind}:{name}:{line}";

    private static string DirOf(string relativePath)
    {
        var dir = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? string.Empty;
        return dir;
    }

    /// <summary>Append mapped declarations to the FileNode's children; return the declared symbols.</summary>
    public static List<TerraformSymbol> Map(HclFile file, FileNode fileNode, string workspaceId, string relativePath)
    {
        var symbols = new List<TerraformSymbol>();
        var dir = DirOf(relativePath);

        foreach (var block in file.Blocks)
        {
            switch (block.Type)
            {
                case "resource":
                    AddResourceLike(block, fileNode, workspaceId, relativePath, dir, "resource", symbols);
                    break;
                case "data":
                    AddResourceLike(block, fileNode, workspaceId, relativePath, dir, "data_source", symbols);
                    break;
                case "module":
                    AddModule(block, fileNode, workspaceId, relativePath, dir, symbols);
                    break;
                case "provider":
                    AddSimpleType(block, fileNode, workspaceId, relativePath, dir, "provider",
                        FirstLabel(block) ?? "provider", symbols);
                    break;
                case "variable":
                    AddVariable(block, fileNode, workspaceId, relativePath, dir, symbols);
                    break;
                case "output":
                    AddOutput(block, fileNode, workspaceId, relativePath, dir, symbols);
                    break;
                case "locals":
                    AddLocals(block, fileNode, workspaceId, relativePath, dir, symbols);
                    break;
                case "dependency":
                    // Terragrunt dependency "X" { config_path = } — a cross-unit reference target.
                    // RefKey becomes "dependency.X" so dependency.X[.outputs.Y] traversals resolve.
                    AddSimpleType(block, fileNode, workspaceId, relativePath, dir, "dependency",
                        FirstLabel(block) ?? "dependency", symbols);
                    break;
                // terraform{} / backend / required_providers / include / inputs → no Layer 3 node
                // (handled as project manifest or Layer 2 dependencies).
                default:
                    break;
            }
        }

        return symbols;
    }

    private static string? FirstLabel(HclBlock b) => b.Labels.Count > 0 ? b.Labels[0] : null;
    private static string? SecondLabel(HclBlock b) => b.Labels.Count > 1 ? b.Labels[1] : null;

    private static void AddResourceLike(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, string kind, List<TerraformSymbol> symbols)
    {
        var resourceType = FirstLabel(block) ?? "unknown";
        var name = SecondLabel(block) ?? resourceType;
        var displayName = $"{resourceType}.{name}";

        // resource_type is Terraform-specific metadata.
        var ext = new Dictionary<string, string>
        {
            ["resource_type"] = resourceType
        };

        var id = Urn(workspaceId, relativePath, "Type", displayName, block.Line);
        fileNode.Children.Add(new TypeNode(
            id, displayName, displayName, relativePath, relativePath,
            block.Line, block.Line, 0, 0, kind, ext));

        // Ref keys: resources are referenced as "<type>.<name>"; data sources as "data.<type>.<name>".
        var refKey = kind == "data_source" ? $"data.{resourceType}.{name}" : $"{resourceType}.{name}";
        symbols.Add(new TerraformSymbol(id, refKey, kind, dir, block.Line, resourceType, name));
    }

    private static void AddModule(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, List<TerraformSymbol> symbols)
    {
        var name = FirstLabel(block) ?? "module";
        var ext = new Dictionary<string, string>();

        var source = FindAttrLiteral(block, "source");
        if (!string.IsNullOrEmpty(source)) ext["source"] = source;

        var id = Urn(workspaceId, relativePath, "Type", name, block.Line);
        fileNode.Children.Add(new TypeNode(
            id, name, name, relativePath, relativePath,
            block.Line, block.Line, 0, 0, "module", ext));

        symbols.Add(new TerraformSymbol(id, $"module.{name}", "module", dir, block.Line));
    }

    private static void AddSimpleType(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, string kind, string name, List<TerraformSymbol> symbols)
    {
        // kind is carried by the typed TypeKind arg; no extra Terraform metadata for these blocks.
        var id = Urn(workspaceId, relativePath, "Type", name, block.Line);
        fileNode.Children.Add(new TypeNode(
            id, name, name, relativePath, relativePath,
            block.Line, block.Line, 0, 0, kind, null));

        symbols.Add(new TerraformSymbol(id, $"{kind}.{name}", kind, dir, block.Line));
    }

    private static void AddVariable(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, List<TerraformSymbol> symbols)
    {
        var name = FirstLabel(block) ?? "variable";
        var ext = new Dictionary<string, string>();

        var dflt = FindAttrLiteral(block, "default");
        if (!string.IsNullOrEmpty(dflt)) ext["default"] = dflt;
        var declaredType = FindAttrLiteral(block, "type");

        var id = Urn(workspaceId, relativePath, "Member", name, block.Line);
        fileNode.Children.Add(new MemberNode(
            id, name, name, relativePath, relativePath,
            block.Line, block.Line, 0, 0, "variable", declaredType, ext));

        symbols.Add(new TerraformSymbol(id, $"var.{name}", "variable", dir, block.Line));
    }

    private static void AddOutput(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, List<TerraformSymbol> symbols)
    {
        var name = FirstLabel(block) ?? "output";
        var ext = new Dictionary<string, string>();

        var value = FindAttrLiteral(block, "value");
        if (!string.IsNullOrEmpty(value)) ext["value"] = value;

        var id = Urn(workspaceId, relativePath, "Member", name, block.Line);
        fileNode.Children.Add(new MemberNode(
            id, name, name, relativePath, relativePath,
            block.Line, block.Line, 0, 0, "output", null, ext));

        symbols.Add(new TerraformSymbol(id, $"output.{name}", "output", dir, block.Line));
    }

    private static void AddLocals(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, List<TerraformSymbol> symbols)
    {
        // A locals block has no labels; each attribute is one local value.
        foreach (var attr in block.Attributes)
        {
            var ext = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(attr.Value.Literal)) ext["value"] = attr.Value.Literal!;

            var id = Urn(workspaceId, relativePath, "Member", attr.Name, attr.Line);
            fileNode.Children.Add(new MemberNode(
                id, attr.Name, attr.Name, relativePath, relativePath,
                attr.Line, attr.Line, 0, 0, "local", null, ext));

            symbols.Add(new TerraformSymbol(id, $"local.{attr.Name}", "local", dir, attr.Line));
        }
    }

    private static string? FindAttrLiteral(HclBlock block, string attrName)
    {
        foreach (var a in block.Attributes)
        {
            if (string.Equals(a.Name, attrName, StringComparison.Ordinal))
            {
                return a.Value.Literal;
            }
        }
        return null;
    }

    /// <summary>
    /// Collects CONFIGURES intents from a file's top-level attributes:
    ///   - every assignment in a <c>.tfvars</c> file (target variable in the same module dir);
    ///   - each key of a Terragrunt <c>inputs = { }</c> object (target variable in the wrapped
    ///     module, resolved by the enricher from this unit's <c>terraform { source }</c>).
    /// The enricher resolves each to a declared <c>variable</c> and emits the edge; unresolved
    /// targets (e.g. a remote git module not in the workspace) produce no edge.
    /// </summary>
    public static List<TerraformConfigure> MapConfigures(HclFile file, string relativePath)
    {
        var configures = new List<TerraformConfigure>();
        var fileName = Path.GetFileName(relativePath);

        if (fileName.EndsWith(".tfvars", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var attr in file.TopLevelAttributes)
            {
                configures.Add(new TerraformConfigure(attr.Name, "tfvars", attr.Line));
            }
            return configures;
        }

        // Terragrunt unit: inputs = { key = value, ... } → each key configures the wrapped module.
        var inputs = file.TopLevelAttributes.FirstOrDefault(a => a.Name == "inputs");
        if (inputs?.Value.ObjectKeys is { Count: > 0 } keys)
        {
            // The wrapped module source is the `source` attribute of the unit's `terraform` block.
            var terraform = file.Blocks.FirstOrDefault(b => b.Type == "terraform");
            var rawSource = terraform != null ? FindAttrLiteral(terraform, "source") : null;
            foreach (var key in keys)
            {
                configures.Add(new TerraformConfigure(key, "terragrunt", inputs.Line, rawSource));
            }
        }

        return configures;
    }
}
