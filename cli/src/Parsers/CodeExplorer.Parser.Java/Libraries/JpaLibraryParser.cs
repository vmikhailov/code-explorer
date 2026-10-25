using System.Collections.Frozen;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Java.Libraries;

public class JpaLibraryParser : DeclarativeSemanticExtension
{
    public JpaLibraryParser()
    {
        Id = "jpa";
        Name = "JPA / Hibernate / Spring Data";
        Type = "db:relational";
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.springframework.data.jpa*",
            "org.springframework.data.repository*",
            "jakarta.persistence*",
            "javax.persistence*",
            "org.hibernate*"
        ];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[] { "createQuery", "createNativeQuery", "createNamedQuery" }.ToFrozenSet(StringComparer.Ordinal),
                QueryAnnotationSuffixes = new[] { "Query" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                RequireExtractableSqlOnCall = false,
                FallbackQueryPrefix = "JPA Query"
            }
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { "Table", "Entity" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}

