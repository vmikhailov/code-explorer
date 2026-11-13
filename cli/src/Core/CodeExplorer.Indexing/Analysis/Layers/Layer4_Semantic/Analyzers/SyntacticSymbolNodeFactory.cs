using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Analysis.Layers.Layer4_Semantic.Analyzers;

internal static class SyntacticSymbolNodeFactory
{
    private const string KindClass = "class";
    private const string KindInterface = "interface";
    private const string KindField = "field";
    private const string RoleBusiness = "business";

    public static IOntologyNode? TryCreateLayer3Node(
        string kind,
        string symbolId,
        string name,
        SyntaxNode node,
        SyntacticSymbol syntactic,
        string workspaceId,
        string relativePath,
        bool symbolIsTest,
        ProjectEntityKind? projectKind,
        string? projectName)
    {
        if (kind == OntologyConstants.NodeLabels.Function)
        {
            var stageOrder = syntactic.TryGetProperty(OntologyConstants.PropertyNames.StageOrder, out var so) && int.TryParse(so, out var parsedOrder) ? parsedOrder : (int?)null;
            return new FunctionNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1,
                symbolIsTest, projectKind, projectName, stageOrder);
        }

        if (kind is "Class" or "Struct" or "Enum" || kind == OntologyConstants.NodeLabels.Type)
        {
            var typeKind = syntactic.TryGetProperty(OntologyConstants.PropertyNames.TypeKind, out var tk)
                ? tk
                : (kind == OntologyConstants.NodeLabels.Type ? KindClass : kind.ToLowerInvariant());
            var stageOrder = syntactic.TryGetProperty(OntologyConstants.PropertyNames.StageOrder, out var so) && int.TryParse(so, out var parsedOrder) ? parsedOrder : (int?)null;
            return new TypeNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1, typeKind,
                symbolIsTest, projectKind, projectName, stageOrder);
        }

        if (kind == "Interface")
        {
            var stageOrder = syntactic.TryGetProperty(OntologyConstants.PropertyNames.StageOrder, out var so) && int.TryParse(so, out var parsedOrder) ? parsedOrder : (int?)null;
            return new TypeNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1, KindInterface,
                symbolIsTest, projectKind, projectName, stageOrder);
        }

        if (kind == OntologyConstants.NodeLabels.Member)
        {
            var memberKind = syntactic.TryGetProperty(OntologyConstants.PropertyNames.MemberKind, out var mk) ? mk : KindField;
            var dataType = syntactic.TryGetProperty(OntologyConstants.PropertyNames.DataType, out var dt) ? dt : null;
            return new MemberNode(symbolId, name, symbolId, relativePath, relativePath,
                node.StartPosition.Row + 1, node.EndPosition.Row + 1, node.StartPosition.Column + 1, node.EndPosition.Column + 1,
                memberKind, dataType);
        }

        if (kind == OntologyConstants.NodeLabels.CallLiteral)
        {
            var val = syntactic.Text ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Value) ?? syntactic.Name;
            var callee = syntactic.Callee ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Callee) ?? string.Empty;
            var argIdx = syntactic.ArgIndex != 0 ? syntactic.ArgIndex : (int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.ArgIndex), out var ai) ? ai : 0);
            var line = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Line), out var ln) ? ln : (int)node.StartPosition.Row + 1;
            var col = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Column), out var cl) ? cl : (int)node.StartPosition.Column + 1;
            var role = syntactic.Role ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Role) ?? RoleBusiness;
            var isDecorator = syntactic.IsDecorator || (bool.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.IsDecorator), out var isDec) && isDec);
            var litId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CallLiteral}:{relativePath}:{line}:{col}";
            return new CallLiteralNode(litId, name, val, callee, argIdx, relativePath, line, col, symbolIsTest, projectKind, projectName, role, isDecorator);
        }

        if (kind == OntologyConstants.NodeLabels.CatchClause)
        {
            var exType = syntactic.RequestType ?? syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.ExceptionType) ?? string.Empty;
            var isEmpty = syntactic.IsEmptyCatch || (bool.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.IsEmpty), out var ie) && ie);
            var hasComment = syntactic.HasCatchComment || (bool.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.HasComment), out var hc) && hc);
            var stmtCount = syntactic.ArgIndex != 0 ? syntactic.ArgIndex : (int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.StatementCount), out var sc) ? sc : 0);
            var line = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Line), out var ln) ? ln : (int)node.StartPosition.Row + 1;
            var col = int.TryParse(syntactic.GetPropertyOrDefault(OntologyConstants.PropertyNames.Column), out var cl) ? cl : (int)node.StartPosition.Column + 1;
            var catchId = $"{workspaceId}:{OntologyConstants.IdPrefixes.CatchClause}:{relativePath}:{line}:{col}";
            return new CatchClauseNode(catchId, name, exType, relativePath, line, col, isEmpty, hasComment, stmtCount, symbolIsTest, projectKind, projectName);
        }

        return null;
    }

    public static void AttachCloudHostingIfResolved(
        ExternalServiceNode esCandidate,
        string workspaceId,
        string relativePath,
        ParsingContext? ctx,
        List<Relationship>? relationships)
    {
        if (esCandidate.IsExternal != true ||
            CloudProviderHelper.TryResolve(esCandidate.DomainOrService) is not { } cloudMatch)
        {
            return;
        }

        esCandidate.Provider ??= cloudMatch.DisplayName;
        var cloudId = CloudProviderHelper.BuildCloudNodeId(workspaceId, cloudMatch.ProviderId);
        if (ctx?.SemanticStructure != null && !ctx.SemanticStructure.Children.Any(c => c.Id == cloudId))
        {
            ctx.SemanticStructure.Children.Add(new CloudNode(cloudId, cloudMatch.DisplayName, relativePath)
            {
                Provider = cloudMatch.ProviderId,
                Category = cloudMatch.TierSlug
            });
        }

        var hostedRel = Relationship.FromRelationship(new HostedInRelationship(esCandidate.Id, cloudId)
        {
            FilePath = relativePath,
            Line = esCandidate.StartLine ?? 0
        });
        ctx?.AddGlobalProjectDependency(hostedRel);
        relationships?.Add(hostedRel);
    }
}
