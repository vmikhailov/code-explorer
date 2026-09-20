using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class OrleansLibraryParser : ILibraryParser
{
    public string Name => "Microsoft Orleans";
    public string Id => "orleans";
    public string Type => OntologyConstants.LibraryTypes.Framework;
    public IReadOnlyList<string> SupportedPatterns => ["Orleans", "Microsoft.Orleans", "Microsoft.Orleans.*", "*Orleans*"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    private static readonly HashSet<string> GrainBaseClasses =
    [
        "Grain", "JournaledGrain"
    ];

    private static readonly HashSet<string> GrainMarkerInterfaces =
    [
        "IGrain", "IGrainWithGuidKey", "IGrainWithStringKey", "IGrainWithIntegerKey",
        "IGrainWithGuidCompoundKey", "IGrainWithIntegerCompoundKey", "IGrainObserver", "IGrainExtension"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGrainClass(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsGrainClass(node))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            if (nameNode.IsValid())
            {
                return $"Grain:{nameNode.Text}";
            }
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsGrainClass(node))
        {
            var baseList = node.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
            if (baseList.IsValid())
            {
                foreach (var child in baseList.Children)
                {
                    if (child.Is(TreeSitterSyntax.CSharp.GenericName))
                    {
                        var idNode = child.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                        if (idNode.IsValid() && GrainBaseClasses.Contains(idNode.Text))
                        {
                            var typeArgs = child.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                            if (typeArgs.IsValid())
                            {
                                foreach (var tArg in typeArgs.Children)
                                {
                                    if (tArg.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier))
                                    {
                                        references.Add(new Reference(scopeSymbolId, tArg.Text, OntologyConstants.Relationships.UsesType));
                                    }
                                }
                            }
                        }
                    }
                    else if (child.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier))
                    {
                        var typeName = child.Text;
                        if (typeName.StartsWith('I') && (typeName.EndsWith("Grain") || GrainMarkerInterfaces.Contains(typeName)))
                        {
                            references.Add(new Reference(scopeSymbolId, typeName, OntologyConstants.Relationships.Implements));
                        }
                    }
                }
            }
        }

        if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var methodName = func.GetField(TreeSitterSyntax.Fields.Name);
                if (methodName.IsValid() && methodName.Text == "GetGrain")
                {
                    var typeArgs = node.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList)
                                   ?? func.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                    var grainType = typeArgs?.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
                    if (grainType.IsValid())
                    {
                        references.Add(new Reference(scopeSymbolId, grainType.Text, OntologyConstants.Relationships.Calls));
                    }
                }
            }
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsGrainClass(node))
        {
            symbol.Protocol = "Orleans";
            symbol.OperationType = "Grain";
        }
    }

    private static bool IsGrainClass(Node node)
    {
        if (!node.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            return false;

        var baseList = node.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (!baseList.IsValid()) return false;

        foreach (var child in baseList.Children)
        {
            if (child.Is(TreeSitterSyntax.CSharp.GenericName))
            {
                var idNode = child.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                if (idNode.IsValid() && GrainBaseClasses.Contains(idNode.Text))
                    return true;
            }
            else if (child.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier))
            {
                var typeName = child.Text;
                if (GrainBaseClasses.Contains(typeName) || GrainMarkerInterfaces.Contains(typeName))
                    return true;
                if (typeName.StartsWith('I') && typeName.EndsWith("Grain"))
                    return true;
            }
        }

        return false;
    }
}
