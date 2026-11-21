namespace CodeExplorer.Core.Parser.Sql.Ast;

public abstract record SqlNodeAst;

public sealed record SqlScriptAst(IReadOnlyList<SqlStatementAst> Statements) : SqlNodeAst;

public abstract record SqlStatementAst : SqlNodeAst
{
    public IReadOnlyList<SqlCteAst> Ctes { get; init; } = [];
}

public sealed record SqlTableRefAst(
    string? Database,
    string? Schema,
    string TableName,
    string? Alias = null,
    bool IsTemporary = false,
    bool IsCte = false,
    bool IsSubquery = false,
    bool IsWriteTarget = false
) : SqlNodeAst
{
    public string CanonicalName =>
        !string.IsNullOrEmpty(Database) && !string.IsNullOrEmpty(Schema) ? $"{Database}.{Schema}.{TableName}" :
        (!string.IsNullOrEmpty(Database) ? $"{Database}.{TableName}" :
        (!string.IsNullOrEmpty(Schema) ? $"{Schema}.{TableName}" : TableName));
}

public sealed record SqlProcRefAst(
    string? Database,
    string? Schema,
    string ProcedureName
) : SqlNodeAst
{
    public string CanonicalName => !string.IsNullOrEmpty(Schema) ? $"{Schema}.{ProcedureName}" : ProcedureName;
}

public sealed record SqlCteAst(
    string Name,
    IReadOnlyList<string> Columns,
    SqlStatementAst? InnerStatement
) : SqlNodeAst;

public sealed record SqlSelectStatementAst(
    IReadOnlyList<SqlTableRefAst> Tables,
    IReadOnlyList<SqlTableRefAst> Joins
) : SqlStatementAst;

public sealed record SqlInsertStatementAst(
    SqlTableRefAst TargetTable,
    SqlStatementAst? SourceQuery
) : SqlStatementAst;

public sealed record SqlUpdateStatementAst(
    SqlTableRefAst TargetTable,
    IReadOnlyList<SqlTableRefAst> FromTables
) : SqlStatementAst;

public sealed record SqlDeleteStatementAst(
    SqlTableRefAst TargetTable,
    IReadOnlyList<SqlTableRefAst> UsingTables
) : SqlStatementAst;

public sealed record SqlMergeStatementAst(
    SqlTableRefAst TargetTable,
    SqlTableRefAst? UsingTable
) : SqlStatementAst;

public sealed record SqlForeignKeyAst(
    string? FromColumn,
    SqlTableRefAst TargetTable,
    string? ToColumn = null
) : SqlNodeAst;

public sealed record SqlColumnDefAst(
    string Name,
    string DataType,
    bool IsPrimaryKey = false,
    bool IsNullable = true,
    bool IsUnique = false,
    SqlForeignKeyAst? ForeignKey = null
) : SqlNodeAst;

public enum SqlRoutineKind : byte
{
    Procedure = 0,
    Function = 1,
    Trigger = 2
}

public sealed record SqlCreateTableStatementAst(
    SqlTableRefAst Table,
    bool IsTemporary,
    SqlStatementAst? AsSelectQuery,
    IReadOnlyList<SqlTableRefAst>? ReferencedTables = null,
    IReadOnlyList<SqlColumnDefAst>? Columns = null,
    IReadOnlyList<SqlForeignKeyAst>? ForeignKeys = null
) : SqlStatementAst;

public sealed record SqlAlterTableStatementAst(
    SqlTableRefAst TargetTable,
    IReadOnlyList<SqlTableRefAst> ReferencedTables,
    IReadOnlyList<SqlColumnDefAst>? AddedColumns = null,
    IReadOnlyList<SqlForeignKeyAst>? ForeignKeys = null
) : SqlStatementAst;

public sealed record SqlCreateViewStatementAst(
    SqlTableRefAst View,
    SqlStatementAst? AsSelectQuery,
    bool IsMaterialized = false
) : SqlStatementAst;

public sealed record SqlCreateProcedureStatementAst(
    SqlProcRefAst Procedure,
    string RawBody,
    IReadOnlyList<SqlStatementAst> BodyStatements,
    SqlRoutineKind RoutineKind = SqlRoutineKind.Procedure,
    SqlTableRefAst? TriggerTable = null,
    string? TriggerEvent = null
) : SqlStatementAst;

public enum SqlDropObjectType : byte
{
    Table = 0,
    View = 1,
    Procedure = 2,
    Function = 3,
    Database = 4,
    Schema = 5,
    Index = 6,
    Other = 7
}

public sealed record SqlDropStatementAst(
    SqlDropObjectType DropType,
    SqlTableRefAst Target
) : SqlStatementAst;

public sealed record SqlTruncateStatementAst(
    SqlTableRefAst TargetTable
) : SqlStatementAst;

public sealed record SqlExecStatementAst(
    SqlProcRefAst Procedure
) : SqlStatementAst;
