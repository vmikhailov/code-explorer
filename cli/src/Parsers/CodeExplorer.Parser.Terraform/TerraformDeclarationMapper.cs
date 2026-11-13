using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Parser.Terraform.Hcl;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Maps parsed <see cref="HclFile"/> blocks and attributes into ontology nodes
/// (<see cref="CloudResourceNode"/>, <see cref="CloudNode"/>, <see cref="MemberNode"/>, <see cref="TypeNode"/>)
/// and <see cref="TerraformSymbol"/> declarations.
/// </summary>
public static class TerraformDeclarationMapper
{
    private const string BlockResource = "resource";
    private const string BlockData = "data";
    private const string BlockModule = "module";
    private const string BlockProvider = "provider";
    private const string BlockDependency = "dependency";
    private const string BlockVariable = "variable";
    private const string BlockLocals = "locals";
    private const string BlockOutput = "output";
    private const string BlockTerraform = "terraform";

    private const string AttrSource = "source";
    private const string AttrConfigPath = "config_path";
    private const string AttrAlias = "alias";
    private const string AttrRegion = "region";
    private const string AttrType = "type";
    private const string AttrDefault = "default";
    private const string AttrValue = "value";
    private const string AttrCount = "count";
    private const string AttrForEach = "for_each";
    private const string AttrProviders = "providers";
    private const string AttrDependsOn = "depends_on";
    private const string AttrVersion = "version";

    private const string ModeManaged = "managed";
    private const string ModeData = "data";
    private const string VisibilityPublic = "public";
    private const string VisibilityPrivate = "private";
    private const string AnyType = "any";
    private const string SymKindType = "Type";
    private const string SymKindMember = "Member";

    private static readonly HashSet<string> MetaModuleAttributes = new(StringComparer.Ordinal)
    {
        AttrSource, AttrVersion, AttrCount, AttrForEach, AttrProviders, AttrDependsOn
    };

    public static IReadOnlyList<TerraformSymbol> MapDeclarations(
        HclFile hclFile,
        string workspaceId,
        string fileNodeId,
        string relativeFilePath,
        string moduleDir,
        List<IOntologyNode> nodeSink)
    {
        var symbols = new List<TerraformSymbol>();

        foreach (var block in hclFile.Blocks)
        {
            switch (block.BlockHeader)
            {
                case BlockResource when block.Labels.Count >= 2:
                    MapResourceBlock(block, workspaceId, fileNodeId, relativeFilePath, moduleDir, symbols, nodeSink);
                    break;

                case BlockData when block.Labels.Count >= 2:
                    MapDataBlock(block, workspaceId, fileNodeId, relativeFilePath, moduleDir, symbols, nodeSink);
                    break;

                case BlockModule when block.Labels.Count >= 1:
                    MapModuleBlock(block, workspaceId, fileNodeId, relativeFilePath, moduleDir, symbols, nodeSink);
                    break;

                case BlockProvider when block.Labels.Count >= 1:
                    MapProviderBlock(block, workspaceId, fileNodeId, relativeFilePath, moduleDir, symbols, nodeSink);
                    break;

                case BlockDependency when block.Labels.Count >= 1:
                    MapDependencyBlock(block, workspaceId, fileNodeId, relativeFilePath, moduleDir, symbols, nodeSink);
                    break;

                case BlockVariable when block.Labels.Count >= 1:
                    MapVariableBlock(block, workspaceId, fileNodeId, relativeFilePath, moduleDir, symbols, nodeSink);
                    break;

                case BlockLocals:
                    MapLocalsBlock(block, workspaceId, fileNodeId, relativeFilePath, moduleDir, symbols, nodeSink);
                    break;

                case BlockOutput when block.Labels.Count >= 1:
                    MapOutputBlock(block, workspaceId, fileNodeId, relativeFilePath, moduleDir, symbols, nodeSink);
                    break;
            }
        }

        return symbols;
    }

    private static void MapResourceBlock(
        HclBlock block,
        string workspaceId,
        string fileNodeId,
        string relativeFilePath,
        string moduleDir,
        List<TerraformSymbol> symbols,
        List<IOntologyNode> nodeSink)
    {
        var rType = block.Labels[0];
        var rName = block.Labels[1];
        var canonical = $"{rType}.{rName}";
        var nodeId = BuildCloudResourceId(workspaceId, moduleDir, canonical);
        var classification = CloudResourceCatalog.Classify(rType);

        var resourceNode = new CloudResourceNode(nodeId, canonical, rType, relativeFilePath)
        {
            Provider = classification.Provider,
            ResourceRole = classification.ResourceRole,
            Mode = ModeManaged,
            FilePath = relativeFilePath,
            Line = block.StartLine,
            LineEnd = block.EndLine
        };
        nodeSink.Add(resourceNode);

        symbols.Add(new TerraformSymbol(
            TerraformSymbolKind.Resource,
            canonical,
            rType,
            rName,
            nodeId,
            fileNodeId,
            relativeFilePath,
            moduleDir,
            block.StartLine,
            block.EndLine,
            block.CollectReferences()));
    }

    private static void MapDataBlock(
        HclBlock block,
        string workspaceId,
        string fileNodeId,
        string relativeFilePath,
        string moduleDir,
        List<TerraformSymbol> symbols,
        List<IOntologyNode> nodeSink)
    {
        var dType = block.Labels[0];
        var dName = block.Labels[1];
        var canonical = $"{BlockData}.{dType}.{dName}";
        var nodeId = BuildCloudResourceId(workspaceId, moduleDir, canonical);
        var classification = CloudResourceCatalog.Classify(dType);

        var dataNode = new CloudResourceNode(nodeId, canonical, dType, relativeFilePath)
        {
            Provider = classification.Provider,
            ResourceRole = classification.ResourceRole,
            Mode = ModeData,
            FilePath = relativeFilePath,
            Line = block.StartLine,
            LineEnd = block.EndLine
        };
        nodeSink.Add(dataNode);

        symbols.Add(new TerraformSymbol(
            TerraformSymbolKind.DataSource,
            canonical,
            dType,
            dName,
            nodeId,
            fileNodeId,
            relativeFilePath,
            moduleDir,
            block.StartLine,
            block.EndLine,
            block.CollectReferences()));
    }

    private static void MapModuleBlock(
        HclBlock block,
        string workspaceId,
        string fileNodeId,
        string relativeFilePath,
        string moduleDir,
        List<TerraformSymbol> symbols,
        List<IOntologyNode> nodeSink)
    {
        var mName = block.Labels[0];
        var canonical = $"{BlockModule}.{mName}";
        var nodeId = BuildSymbolId(workspaceId, relativeFilePath, SymKindType, canonical);

        var inputs = new Dictionary<string, IReadOnlyList<HclReference>>(StringComparer.Ordinal);
        string? sourceRaw = null;
        foreach (var attr in block.Attributes)
        {
            if (string.Equals(attr.Name, AttrSource, StringComparison.Ordinal))
            {
                sourceRaw = attr.Value.TryGetStringLiteral();
                continue;
            }
            if (MetaModuleAttributes.Contains(attr.Name))
            {
                continue;
            }
            inputs[attr.Name] = attr.Value.CollectReferences(attr.Name);
        }

        nodeSink.Add(new TypeNode(
            nodeId,
            canonical,
            canonical,
            relativeFilePath,
            relativeFilePath,
            block.StartLine,
            block.EndLine,
            0,
            0,
            TypeKind.Module));

        symbols.Add(new TerraformSymbol(
            TerraformSymbolKind.Module,
            canonical,
            BlockModule,
            mName,
            nodeId,
            fileNodeId,
            relativeFilePath,
            moduleDir,
            block.StartLine,
            block.EndLine,
            block.CollectReferences(),
            inputs,
            sourceRaw));
    }

    private static void MapProviderBlock(
        HclBlock block,
        string workspaceId,
        string fileNodeId,
        string relativeFilePath,
        string moduleDir,
        List<TerraformSymbol> symbols,
        List<IOntologyNode> nodeSink)
    {
        var pName = block.Labels[0];
        var alias = block.FindAttribute(AttrAlias)?.Value.TryGetStringLiteral();
        var region = block.FindAttribute(AttrRegion)?.Value.TryGetStringLiteral();
        var canonical = string.IsNullOrEmpty(alias)
            ? $"{BlockProvider}.{pName}"
            : $"{BlockProvider}.{pName}.{alias}";

        var providerMatch = CloudProviderHelper.ResolveOrFallback(pName);
        var cloudId = CloudProviderHelper.BuildCloudNodeId(workspaceId, providerMatch.ProviderId);
        var cloudNode = new CloudNode(cloudId, providerMatch.DisplayName, relativeFilePath)
        {
            Provider = providerMatch.ProviderId,
            Region = region,
            Category = providerMatch.TierSlug
        };
        nodeSink.Add(cloudNode);

        symbols.Add(new TerraformSymbol(
            TerraformSymbolKind.Provider,
            canonical,
            pName,
            pName,
            cloudId,
            fileNodeId,
            relativeFilePath,
            moduleDir,
            block.StartLine,
            block.EndLine,
            block.CollectReferences()));
    }

    private static void MapDependencyBlock(
        HclBlock block,
        string workspaceId,
        string fileNodeId,
        string relativeFilePath,
        string moduleDir,
        List<TerraformSymbol> symbols,
        List<IOntologyNode> nodeSink)
    {
        var depName = block.Labels[0];
        var canonical = $"{BlockDependency}.{depName}";
        var nodeId = BuildSymbolId(workspaceId, relativeFilePath, SymKindType, canonical);
        var configPath = block.FindAttribute(AttrConfigPath)?.Value.TryGetStringLiteral();

        nodeSink.Add(new TypeNode(
            nodeId,
            canonical,
            canonical,
            relativeFilePath,
            relativeFilePath,
            block.StartLine,
            block.EndLine,
            0,
            0,
            TypeKind.Dependency));

        symbols.Add(new TerraformSymbol(
            TerraformSymbolKind.Dependency,
            canonical,
            BlockDependency,
            depName,
            nodeId,
            fileNodeId,
            relativeFilePath,
            moduleDir,
            block.StartLine,
            block.EndLine,
            block.CollectReferences(),
            ModuleSourceRaw: configPath));
    }

    private static void MapVariableBlock(
        HclBlock block,
        string workspaceId,
        string fileNodeId,
        string relativeFilePath,
        string moduleDir,
        List<TerraformSymbol> symbols,
        List<IOntologyNode> nodeSink)
    {
        var vName = block.Labels[0];
        var canonical = $"var.{vName}";
        var nodeId = BuildSymbolId(workspaceId, relativeFilePath, SymKindMember, canonical);
        var typeText = FormatVariableType(block.FindAttribute(AttrType)?.Value);
        var defaultText = block.FindAttribute(AttrDefault)?.Value.TryGetStringLiteral();

        var memberNode = new MemberNode(
            nodeId,
            canonical,
            canonical,
            relativeFilePath,
            relativeFilePath,
            block.StartLine,
            block.EndLine,
            0,
            0,
            MemberKind.Variable,
            typeText)
        {
            InitializerExpression = defaultText
        };
        nodeSink.Add(memberNode);

        symbols.Add(new TerraformSymbol(
            TerraformSymbolKind.Variable,
            canonical,
            BlockVariable,
            vName,
            nodeId,
            fileNodeId,
            relativeFilePath,
            moduleDir,
            block.StartLine,
            block.EndLine,
            block.CollectReferences()));
    }

    private static void MapLocalsBlock(
        HclBlock block,
        string workspaceId,
        string fileNodeId,
        string relativeFilePath,
        string moduleDir,
        List<TerraformSymbol> symbols,
        List<IOntologyNode> nodeSink)
    {
        foreach (var attr in block.Attributes)
        {
            var canonical = $"local.{attr.Name}";
            var nodeId = BuildSymbolId(workspaceId, relativeFilePath, SymKindMember, canonical);
            var refs = attr.Value.CollectReferences(attr.Name);

            var memberNode = new MemberNode(
                nodeId,
                canonical,
                canonical,
                relativeFilePath,
                relativeFilePath,
                attr.Line,
                attr.Line,
                0,
                0,
                MemberKind.Local,
                AnyType)
            {
                InitializerExpression = attr.Value.TryGetStringLiteral()
            };
            nodeSink.Add(memberNode);

            symbols.Add(new TerraformSymbol(
                TerraformSymbolKind.Local,
                canonical,
                BlockLocals,
                attr.Name,
                nodeId,
                fileNodeId,
                relativeFilePath,
                moduleDir,
                attr.Line,
                attr.Line,
                refs));
        }
    }

    private static void MapOutputBlock(
        HclBlock block,
        string workspaceId,
        string fileNodeId,
        string relativeFilePath,
        string moduleDir,
        List<TerraformSymbol> symbols,
        List<IOntologyNode> nodeSink)
    {
        var oName = block.Labels[0];
        var canonical = $"{BlockOutput}.{oName}";
        var nodeId = BuildSymbolId(workspaceId, relativeFilePath, SymKindMember, canonical);
        var valueAttr = block.FindAttribute(AttrValue);

        var memberNode = new MemberNode(
            nodeId,
            canonical,
            canonical,
            relativeFilePath,
            relativeFilePath,
            block.StartLine,
            block.EndLine,
            0,
            0,
            MemberKind.Output,
            AnyType)
        {
            InitializerExpression = valueAttr?.Value.TryGetStringLiteral()
        };
        nodeSink.Add(memberNode);

        symbols.Add(new TerraformSymbol(
            TerraformSymbolKind.Output,
            canonical,
            BlockOutput,
            oName,
            nodeId,
            fileNodeId,
            relativeFilePath,
            moduleDir,
            block.StartLine,
            block.EndLine,
            block.CollectReferences()));
    }

    public static string BuildCloudResourceId(string workspaceId, string moduleDir, string canonicalAddress)
    {
        var normDir = string.IsNullOrEmpty(moduleDir) ? "." : moduleDir.Replace('\\', '/').Trim('/');
        return $"{workspaceId}:{OntologyConstants.IdPrefixes.Project}:{normDir}:{OntologyConstants.IdPrefixes.CloudResource}:{canonicalAddress}";
    }

    public static string BuildSymbolId(string workspaceId, string relativeFilePath, string kind, string canonicalName) =>
        $"{workspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativeFilePath}:{kind}:{canonicalName}";

    public static string GetModuleDir(string relativeFilePath)
    {
        var normalized = relativeFilePath.Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : normalized[..lastSlash];
    }

    private static string FormatVariableType(HclExpr? typeExpr) => typeExpr switch
    {
        HclExpr.Traversal tr => tr.Joined,
        HclExpr.Call call => call.FunctionName,
        HclExpr.Literal lit => lit.Value,
        _ => AnyType
    };
}
