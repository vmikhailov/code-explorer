using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parser.Terraform.Hcl;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Maps HCL blocks → ce Layer 3 nodes (TypeNode / MemberNode) with typed kinds and workspace-scoped
/// URN IDs.
///
/// URN: {workspaceId}:sym:{relativePath}:{Kind}:{name}:{line}  (prevents cross-module collisions)
///
/// Block → node:
///   resource "T" "N"  -> TypeNode   TypeKind.Resource     name=T.N
///   data "T" "N"      -> TypeNode   TypeKind.DataSource   name=T.N
///   module "N"        -> TypeNode   TypeKind.Module       name=N
///   provider "N"      -> TypeNode   TypeKind.Provider     name=N
///   dependency "N"    -> TypeNode   TypeKind.Dependency   name=N  (Terragrunt)
///   variable "N"      -> MemberNode MemberKind.Parameter  (a module input; default → initializer)
///   locals { X=... }  -> MemberNode MemberKind.Variable   (one per entry; value → initializer)
///   output "N"        -> MemberNode MemberKind.Output     (value → initializer)
///   attribute anchor  -> MemberNode MemberKind.Property   (e.g. Terragrunt inputs; value → initializer)
///   .tfvars X = ...   -> MemberNode MemberKind.Property   (value → initializer)
///   terraform { }     -> no node (manifest props only)
///
/// Returns the declared <see cref="TerraformSymbol"/>s so the Layer 4 enricher can resolve
/// traversal references to them.
/// </summary>
public static class TerraformDeclarationMapper
{
    public static string Urn(string workspaceId, string relativePath, string kind, string name, int line)
        => $"{workspaceId}:sym:{relativePath}:{kind}:{name}:{line}";

    private static string DirOf(string relativePath) => Path.GetDirectoryName(relativePath).NormalizeSlashes();

    // URN segments naming the node type; references use the ref-key prefixes below.
    private const string TypeSegment = "Type";
    private const string MemberSegment = "Member";
    private const string ProviderKeyword = "provider";
    private const string DependencyKeyword = "dependency";

    /// <summary>Append mapped declarations to the FileNode's children; return the declared symbols.</summary>
    public static List<TerraformSymbol> Map(HclFile file, FileNode fileNode, string workspaceId, string relativePath)
    {
        var symbols = new List<TerraformSymbol>();
        var dir = DirOf(relativePath);

        foreach (var block in file.Blocks)
        {
            switch (block.Kind)
            {
                case HclBlockKind.Resource:
                    AddResourceLike(block, fileNode, workspaceId, relativePath, dir, TerraformSymbolKind.Resource, symbols);
                    break;
                case HclBlockKind.Data:
                    AddResourceLike(block, fileNode, workspaceId, relativePath, dir, TerraformSymbolKind.DataSource, symbols);
                    break;
                case HclBlockKind.Module:
                    AddModule(block, fileNode, workspaceId, relativePath, dir, symbols);
                    break;
                case HclBlockKind.Provider:
                    AddSimpleType(block, fileNode, workspaceId, relativePath, dir, TerraformSymbolKind.Provider, ProviderKeyword,
                        FirstLabel(block) ?? ProviderKeyword, symbols);
                    break;
                case HclBlockKind.Variable:
                    AddVariable(block, fileNode, workspaceId, relativePath, dir, symbols);
                    break;
                case HclBlockKind.Output:
                    AddOutput(block, fileNode, workspaceId, relativePath, dir, symbols);
                    break;
                case HclBlockKind.Locals:
                    AddLocals(block, fileNode, workspaceId, relativePath, dir, symbols);
                    break;
                case HclBlockKind.Dependency:
                    // Terragrunt dependency "X" { config_path = } — a cross-unit reference target.
                    // RefKey becomes "dependency.X" so dependency.X[.outputs.Y] traversals resolve.
                    AddSimpleType(block, fileNode, workspaceId, relativePath, dir, TerraformSymbolKind.Dependency, DependencyKeyword,
                        FirstLabel(block) ?? DependencyKeyword, symbols);
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

    private static TypeKind ToTypeKind(TerraformSymbolKind kind) => kind switch
    {
        TerraformSymbolKind.Resource => TypeKind.Resource,
        TerraformSymbolKind.DataSource => TypeKind.DataSource,
        TerraformSymbolKind.Module => TypeKind.Module,
        TerraformSymbolKind.Provider => TypeKind.Provider,
        TerraformSymbolKind.Dependency => TypeKind.Dependency,
        _ => TypeKind.Unspecified
    };

    private static void AddResourceLike(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, TerraformSymbolKind kind, List<TerraformSymbol> symbols)
    {
        var resourceType = FirstLabel(block) ?? "unknown";
        var name = SecondLabel(block) ?? resourceType;
        var displayName = $"{resourceType}.{name}";

        var id = Urn(workspaceId, relativePath, TypeSegment, displayName, block.Line);
        fileNode.Children.Add(new TypeNode(
            id, displayName, displayName, relativePath, relativePath,
            block.Line, block.Line, 0, 0, ToTypeKind(kind)));

        // Ref keys: resources are referenced as "<type>.<name>"; data sources as "data.<type>.<name>".
        var refKey = kind == TerraformSymbolKind.DataSource ? $"data.{resourceType}.{name}" : $"{resourceType}.{name}";
        var type = new TerraformResourceType(resourceType);
        var (deployedName, engine) = kind == TerraformSymbolKind.Resource ? DeployedIdentity(block, type) : (null, null);
        symbols.Add(new TerraformSymbol(id, refKey, kind, dir, block.Line, type, name, deployedName, engine));
    }

    // The cloud resource's own name and engine, when written as literals. The name comes from the
    // first name attribute the resource sets; a computed value (interpolation, variable) yields null.
    private static (string? Name, string? Engine) DeployedIdentity(HclBlock block, TerraformResourceType type)
    {
        var classification = CloudResourceCatalog.Classify(type);
        if (classification.Kind == CloudResourceCatalog.SemanticKind.None) return (null, null);

        string? deployedName = null;
        foreach (var attribute in CloudResourceCatalog.NameAttributesFor(classification))
        {
            if (FindAttr(block, attribute) is not { } found) continue;
            deployedName = NullIfBlank((found.Value as HclTemplate)?.LiteralValue);
            break;
        }

        string? engine = null;
        if (classification.Kind == CloudResourceCatalog.SemanticKind.Database)
        {
            var engineAttribute = CloudResourceCatalog.EngineAttributeFor(type);
            var engineValue = engineAttribute == null ? null : (FindAttr(block, engineAttribute)?.Value as HclTemplate)?.LiteralValue;
            engine = CloudResourceCatalog.EngineFor(type, engineValue);
        }

        return (deployedName, engine);
    }

    private static void AddModule(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, List<TerraformSymbol> symbols)
    {
        var name = FirstLabel(block) ?? "module";

        var id = Urn(workspaceId, relativePath, TypeSegment, name, block.Line);
        fileNode.Children.Add(new TypeNode(
            id, name, name, relativePath, relativePath,
            block.Line, block.Line, 0, 0, TypeKind.Module));

        symbols.Add(new TerraformSymbol(id, $"module.{name}", TerraformSymbolKind.Module, dir, block.Line));
    }

    // Provider and Terragrunt dependency blocks: one label, referenced as "<refPrefix>.<name>".
    private static void AddSimpleType(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, TerraformSymbolKind kind, string refPrefix, string name, List<TerraformSymbol> symbols)
    {
        var id = Urn(workspaceId, relativePath, TypeSegment, name, block.Line);
        fileNode.Children.Add(new TypeNode(
            id, name, name, relativePath, relativePath,
            block.Line, block.Line, 0, 0, ToTypeKind(kind)));

        symbols.Add(new TerraformSymbol(id, $"{refPrefix}.{name}", kind, dir, block.Line));
    }

    // A Terraform variable is a module input, so it maps to MemberKind.Parameter; its default
    // (if any) is the initializer.
    private static void AddVariable(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, List<TerraformSymbol> symbols)
    {
        var name = FirstLabel(block) ?? "variable";
        var declaredType = FindAttrText(block, TypeAttribute);

        var id = Urn(workspaceId, relativePath, MemberSegment, name, block.Line);
        fileNode.Children.Add(new MemberNode(
            id, name, name, relativePath, relativePath,
            block.Line, block.Line, 0, 0, MemberKind.Parameter, declaredType)
        {
            InitializerExpression = Bounded(FindAttrText(block, DefaultAttribute))
        });

        symbols.Add(new TerraformSymbol(id, $"var.{name}", TerraformSymbolKind.Variable, dir, block.Line));
    }

    private static void AddOutput(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, List<TerraformSymbol> symbols)
    {
        var name = FirstLabel(block) ?? "output";

        var id = Urn(workspaceId, relativePath, MemberSegment, name, block.Line);
        fileNode.Children.Add(new MemberNode(
            id, name, name, relativePath, relativePath,
            block.Line, block.Line, 0, 0, MemberKind.Output)
        {
            InitializerExpression = Bounded(FindAttrText(block, ValueAttribute))
        });

        symbols.Add(new TerraformSymbol(id, $"output.{name}", TerraformSymbolKind.Output, dir, block.Line));
    }

    private static void AddLocals(HclBlock block, FileNode fileNode, string workspaceId, string relativePath, string dir, List<TerraformSymbol> symbols)
    {
        // A locals block has no labels; each attribute is one local value.
        foreach (var attr in block.Attributes)
        {
            var id = Urn(workspaceId, relativePath, MemberSegment, attr.Name, attr.Line);
            fileNode.Children.Add(new MemberNode(
                id, attr.Name, attr.Name, relativePath, relativePath,
                attr.Line, attr.Line, 0, 0, MemberKind.Variable)
            {
                InitializerExpression = Bounded(attr.ValueText)
            });

            symbols.Add(new TerraformSymbol(id, $"local.{attr.Name}", TerraformSymbolKind.Local, dir, attr.Line));
        }
    }

    // Initializer text follows the other parsers: the value's source text, omitted when empty or
    // longer than MaxInitializerLength.
    private const int MaxInitializerLength = 512;

    private const string TypeAttribute = "type";
    private const string DefaultAttribute = "default";
    private const string ValueAttribute = "value";

    private static string? Bounded(string? text) =>
        string.IsNullOrEmpty(text) || text.Length > MaxInitializerLength ? null : text;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static HclAttribute? FindAttr(HclBlock block, string attrName) =>
        block.Attributes.FirstOrDefault(a => string.Equals(a.Name, attrName, StringComparison.Ordinal));

    private static string? FindAttrText(HclBlock block, string attrName)
    {
        foreach (var a in block.Attributes)
        {
            if (string.Equals(a.Name, attrName, StringComparison.Ordinal))
            {
                return a.ValueText;
            }
        }
        return null;
    }

    /// <summary>
    /// Declares a member node for each attribute anchor (an attribute that makes references but
    /// belongs to no declaration, e.g. Terragrunt <c>inputs</c>) and returns its symbols.
    /// </summary>
    public static List<TerraformSymbol> MapAnchors(IReadOnlyList<TerraformAnchor> anchors, FileNode fileNode, string workspaceId, string relativePath)
    {
        var symbols = new List<TerraformSymbol>();
        var dir = DirOf(relativePath);
        foreach (var anchor in anchors)
        {
            var id = Urn(workspaceId, relativePath, MemberSegment, anchor.Name, anchor.Line);
            fileNode.Children.Add(new MemberNode(
                id, anchor.Name, anchor.Name, relativePath, relativePath,
                anchor.Line, anchor.Line, 0, 0, MemberKind.Property)
            {
                InitializerExpression = Bounded(anchor.ValueText)
            });

            symbols.Add(new TerraformSymbol(id, anchor.Key, TerraformSymbolKind.Attribute, dir, anchor.Line));
        }
        return symbols;
    }

    /// <summary>
    /// Declares a member node for each top-level assignment in a <c>.tfvars</c> file, with the
    /// assigned value as its initializer, and returns them as <see cref="TerraformConfigure"/>s for
    /// the enricher to link to the variable of the same name.
    /// </summary>
    public static List<TerraformConfigure> MapConfigures(HclFile file, FileNode fileNode, string workspaceId, string relativePath)
    {
        var configures = new List<TerraformConfigure>();
        if (!IsTfvars(relativePath))
        {
            return configures;
        }

        foreach (var attr in file.Attributes)
        {
            var id = Urn(workspaceId, relativePath, MemberSegment, attr.Name, attr.Line);
            fileNode.Children.Add(new MemberNode(
                id, attr.Name, attr.Name, relativePath, relativePath,
                attr.Line, attr.Line, 0, 0, MemberKind.Property)
            {
                InitializerExpression = Bounded(attr.ValueText)
            });

            configures.Add(new TerraformConfigure(id, attr.Name, attr.Line));
        }
        return configures;
    }

    public static bool IsTfvars(string relativePath) =>
        relativePath.EndsWith(TfvarsExtension, StringComparison.OrdinalIgnoreCase);

    private const string TfvarsExtension = ".tfvars";
}
