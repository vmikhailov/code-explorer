using System.Collections.Frozen;

namespace CodeExplorer.Parser.Go.Libraries;

public class CasbinGoLibraryParser : DeclarativeSemanticExtension
{
    public CasbinGoLibraryParser()
    {
        Id = "casbin-go";
        Name = "Casbin (Go RBAC/ABAC)";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["github.com/casbin/casbin*"];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[] { "Enforce", "AddPolicy", "RemovePolicy", "LoadPolicy" }.ToFrozenSet(StringComparer.Ordinal),
                StaticTargetName = "casbin_rule"
            }
        ];
    }
}
