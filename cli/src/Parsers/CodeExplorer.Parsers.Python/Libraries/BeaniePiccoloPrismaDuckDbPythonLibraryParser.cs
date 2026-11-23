using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Python.Libraries;

public class BeaniePiccoloPrismaDuckDbPythonLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "beanie-piccolo-prisma-duckdb-python";
    private const string ExtensionName = "Beanie, MongoEngine, Piccolo ORM, Prisma-Client-Py, DuckDB & Alembic";

    public BeaniePiccoloPrismaDuckDbPythonLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "beanie*",
            "mongoengine*",
            "piccolo*",
            "prisma*",
            "duckdb*",
            "alembic*"
        ];
        ClassHierarchySinkRules =
        [
            new ClassHierarchySinkRule
            {
                BaseTypeSubstrings = new[] { "Document", "DynamicDocument", "Table" }.ToFrozenSet(StringComparer.Ordinal),
                EmitTableNode = true,
                TargetRelation = OntologyConstants.Relationships.PersistedIn
            }
        ];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                CallMethodNames = new[] { "sql", "execute", "query_raw", "execute_raw" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackQueryPrefix = "Python SQL"
            }
        ];
    }
}
