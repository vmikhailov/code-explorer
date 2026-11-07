using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class EffectTsLibraryParser : DeclarativeSemanticExtension
{
    public EffectTsLibraryParser()
    {
        Id = "effect-ts";
        Name = "Effect-TS & @effect/platform";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["effect", "effect/*", "@effect/*"];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["get"] = "GET",
                    ["post"] = "POST",
                    ["put"] = "PUT",
                    ["del"] = "DELETE",
                    ["patch"] = "PATCH"
                }.ToFrozenDictionary(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "HttpRouter" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "succeed", "effect", "scoped" }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "Layer" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.DependsOn
            }
        ];
    }
}
