using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class MyBatisPlusQueryDslJavaLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "mybatis-plus-querydsl";
    private const string ExtensionName = "MyBatis-Plus, QueryDSL & Ebean";
    private const string TableNameAnnotation = "TableName";
    private const string ValueKey = "value";

    public MyBatisPlusQueryDslJavaLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "com.baomidou.mybatisplus*",
            "com.querydsl*",
            "io.ebean*",
            "com.blazebit.persistence*"
        ];
        OrmTableRules =
        [
            new OrmTableRule
            {
                TableAnnotationNames = new[] { TableNameAnnotation }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                NameArgumentKeys = new[] { ValueKey }.ToFrozenSet()
            }
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[]
                {
                    "selectById", "selectList", "selectOne", "selectPage", "selectCount",
                    "insert", "updateById", "deleteById", "deleteBatchIds", "selectFrom"
                }.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = new[] { "mapper", "queryFactory", "jpaQueryFactory", "ebean", "DB" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
