using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class PlaywrightCypressLibraryParser : DeclarativeSemanticExtension
{
    public PlaywrightCypressLibraryParser()
    {
        Id = "playwright-cypress";
        Name = "Playwright & Cypress E2E";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.TestFramework;
        SupportedPatterns =
        [
            "@playwright/test*",
            "playwright*",
            "cypress*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "goto", "visit", "intercept", "get", "post", "put", "delete" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.Calls,
                RequireUrlOrPathArgument = true
            }
        ];
    }
}
