using System.Collections.Frozen;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Java.Libraries;

/// <summary>
/// Semantic extension for Spring background scheduling (@Scheduled), async execution (@Async),
/// and declarative caching (@Cacheable, @CacheEvict, @CachePut, @Caching).
/// Emits EntryPoint nodes for @Scheduled workers so background services are not flagged as dead code.
/// </summary>
public class SpringRuntimeLibraryParser : DeclarativeSemanticExtension
{
    private const string ScheduledAnnotation = "Scheduled";
    private const string ScheduledProtocol = "SCHEDULED";
    private const string ScheduledPrefix = "SCHEDULED:";
    private const string UnknownMethodName = "job";

    private static readonly FrozenSet<string> ScheduleArgumentKeys = new[]
    {
        "cron", "fixedRate", "fixedRateString", "fixedDelay", "fixedDelayString", "value"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly string[] AnnotationTypes = ["annotation", "marker_annotation"];

    public SpringRuntimeLibraryParser()
    {
        Id = "spring-runtime";
        Name = "Spring Scheduling, Async & Caching";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns =
        [
            "org.springframework.scheduling.*",
            "org.springframework.cache.*",
            "org.springframework.boot.autoconfigure*",
            "*spring-context*"
        ];
        AnnotationSinkRules =
        [
            new AnnotationSinkRule
            {
                AnnotationNames = new[] { "Async" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.Calls,
                TargetPrefix = "spring-async:"
            },
            new AnnotationSinkRule
            {
                AnnotationNames = new[] { "Cacheable", "CacheEvict", "CachePut", "Caching" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.UsesDb,
                TargetPrefix = "spring-cache:"
            }
        ];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = AnnotationTypes,
                MapNodeType = MapScheduledNodeType,
                ExtractIdentifier = ExtractScheduledIdentifier,
                EnrichSymbol = EnrichScheduledSymbol
            }
        ];
    }

    private static bool IsScheduledAnnotation(Node node)
    {
        var shortName = ExtractShortAnnotationName(node);
        return !string.IsNullOrEmpty(shortName) &&
               shortName.Equals(ScheduledAnnotation, StringComparison.OrdinalIgnoreCase);
    }

    private static string? MapScheduledNodeType(Node node, ParsingContext ctx) =>
        IsScheduledAnnotation(node) ? OntologyConstants.NodeLabels.EntryPoint : null;

    private static string? ExtractScheduledIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (!IsScheduledAnnotation(node)) return null;

        var methodName = ResolveEnclosingMethodName(node) ?? UnknownMethodName;
        var scheduleExpr = ExtractScheduleArgument(node);
        return !string.IsNullOrEmpty(scheduleExpr)
            ? $"{ScheduledPrefix}{methodName} ({scheduleExpr})"
            : $"{ScheduledPrefix}{methodName}";
    }

    private static void EnrichScheduledSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsScheduledAnnotation(node))
        {
            symbol.Protocol = ScheduledProtocol;
        }
    }

    private static string? ExtractShortAnnotationName(Node node)
    {
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name)
                       ?? node.Children.FirstOrDefault(c => c is IdentifierExprSyntax or MemberAccessExprSyntax);
        if (!nameNode.IsValid()) return null;

        var raw = nameNode.Text.TrimStart('@').Trim();
        var dot = raw.LastIndexOf('.');
        return dot >= 0 && dot + 1 < raw.Length ? raw[(dot + 1)..] : raw;
    }

    private static string? ExtractScheduleArgument(Node node)
    {
        foreach (var child in node.Children)
        {
            if (child is not SyntaxList { Kind: SyntaxListKind.Arguments }) continue;
            foreach (var arg in child.Children)
            {
                if (arg is KeyValueExprSyntax)
                {
                    var keyNode = arg.Children.FirstOrDefault(c => c is IdentifierExprSyntax);
                    if (keyNode.IsValid() && ScheduleArgumentKeys.Contains(keyNode.Text))
                    {
                        var valNode = arg.Children.FirstOrDefault(c => c is LiteralExprSyntax);
                        if (valNode.IsValid()) return valNode.Text.Trim('"', '\'').Trim();
                    }
                }
                else if (arg is LiteralExprSyntax)
                {
                    return arg.Text.Trim('"', '\'').Trim();
                }
            }
        }
        return null;
    }

    private static string? ResolveEnclosingMethodName(Node annotationNode)
    {
        var parent = annotationNode.Parent;
        while (parent.IsValid())
        {
            if (parent is FunctionDeclSyntax fnDecl && !string.IsNullOrEmpty(fnDecl.Name))
            {
                return fnDecl.Name;
            }
            if (parent.Type == TreeSitterSyntax.Java.MethodDeclaration)
            {
                var nameNode = parent.GetField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid() && !string.IsNullOrEmpty(nameNode.Text))
                {
                    return nameNode.Text;
                }
            }
            if (parent is TypeDeclSyntax)
            {
                break;
            }
            parent = parent.Parent;
        }
        return null;
    }
}
