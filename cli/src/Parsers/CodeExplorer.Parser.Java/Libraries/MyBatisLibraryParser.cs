using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class MyBatisLibraryParser : DeclarativeSemanticExtension
{
    public MyBatisLibraryParser()
    {
        Id = "mybatis";
        Name = "MyBatis & MyBatis-Plus";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.apache.ibatis.*",
            "org.mybatis.*",
            "com.baomidou.mybatisplus.*"
        ];
        SqlSinkRules =
        [
            new SqlSinkRule
            {
                QueryAnnotationSuffixes = new[] { "Select", "Insert", "Update", "Delete" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                CallMethodNames = new[] { "selectOne", "selectList", "selectMap", "insert", "update", "delete" }.ToFrozenSet(StringComparer.Ordinal),
                FallbackQueryPrefix = "MyBatis SQL"
            }
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { "TableName" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                NameArgumentKeys = new[] { "value" }.ToFrozenSet(StringComparer.Ordinal),
                RepositoryInterfaceNames = new[] { "BaseMapper", "IService" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
    }
}
