using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class SpringSecurityAuthJavaLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "spring-security-auth-java";
    private const string ExtensionName = "Spring Security, Sa-Token, Shiro, Casbin & JWT";
    private const string PolicyPrefix = "authz:";

    public SpringSecurityAuthJavaLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "org.springframework.security*",
            "cn.dev33.satoken*",
            "org.apache.shiro*",
            "org.casbin.jcasbin*",
            "org.pac4j*",
            "io.jsonwebtoken*",
            "com.auth0.jwt*"
        ];
        AnnotationSinkRules =
        [
            new AnnotationSinkRule
            {
                AnnotationNames = new[]
                {
                    "PreAuthorize", "PostAuthorize", "Secured", "RolesAllowed",
                    "SaCheckLogin", "SaCheckRole", "SaCheckPermission",
                    "RequiresPermissions", "RequiresRoles", "RequiresAuthentication"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetPrefix = PolicyPrefix,
                TargetRelation = OntologyConstants.Relationships.Calls
            }
        ];
    }
}
