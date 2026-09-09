namespace CodeExplorer.Cypher.Compiler;

public record SqliteCompiledQuery(
    string Sql,
    IReadOnlyDictionary<string, object?> Parameters
);
