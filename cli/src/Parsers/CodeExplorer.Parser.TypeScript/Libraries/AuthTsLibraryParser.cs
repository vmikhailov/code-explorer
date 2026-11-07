using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AuthTsLibraryParser : DeclarativeSemanticExtension
{
    public AuthTsLibraryParser()
    {
        Id = "auth-ts";
        Name = "Auth.js (NextAuth), Better-Auth & Clerk";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "next-auth",
            "next-auth/*",
            "better-auth",
            "better-auth/*",
            "@clerk/nextjs*"
        ];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["NextAuth"] = "ALL",
                    ["betterAuth"] = "ALL",
                    ["clerkMiddleware"] = "ALL"
                }.ToFrozenDictionary(StringComparer.Ordinal),
                StaticRoute = "/api/auth/[...nextauth]"
            }
        ];
    }
}
