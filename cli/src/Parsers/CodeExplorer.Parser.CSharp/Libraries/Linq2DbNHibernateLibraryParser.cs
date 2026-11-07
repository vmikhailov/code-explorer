using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class Linq2DbNHibernateLibraryParser : DeclarativeSemanticExtension
{
    public Linq2DbNHibernateLibraryParser()
    {
        Id = "linq2db-nhibernate";
        Name = "Linq2Db & NHibernate";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["LinqToDB", "LinqToDB.*", "NHibernate", "NHibernate.*", "FluentNHibernate", "FluentNHibernate.*"];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { "Table" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                NameArgumentKeys = new[] { "Name", "value" }.ToFrozenSet(StringComparer.Ordinal),
                RepositoryInterfaceNames = new[] { "IDataContext", "ISession", "IStatelessSession" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
    }
}
