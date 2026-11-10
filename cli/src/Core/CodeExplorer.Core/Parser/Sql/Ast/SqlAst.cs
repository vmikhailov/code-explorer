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

public sealed record SqlCreateTableStatementAst(
    SqlTableRefAst Table,
    bool IsTemporary,
    SqlStatementAst? AsSelectQuery
) : SqlStatementAst;

public sealed record SqlCreateViewStatementAst(
    SqlTableRefAst View,
    SqlStatementAst? AsSelectQuery
) : SqlStatementAst;

public sealed record SqlCreateProcedureStatementAst(
    SqlProcRefAst Procedure,
    string RawBody,
    IReadOnlyList<SqlStatementAst> BodyStatements
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
