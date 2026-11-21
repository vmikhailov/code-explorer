using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Sql.Ast;

namespace CodeExplorer.Parsers.Sql;

internal static class SqlSchemaHelper
{
    private static readonly JsonSerializerOptions ColumnJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal sealed record ColumnSchemaDto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string SqlFormat,
        [property: JsonPropertyName("pk")] bool? IsPrimaryKey = null,
        [property: JsonPropertyName("nullable")] bool? IsNullable = null,
        [property: JsonPropertyName("unique")] bool? IsUnique = null,
        [property: JsonPropertyName("fk")] string? ForeignKey = null);

    public static void MergeColumnsIntoTable(
        TableNode tableNode,
        IReadOnlyList<SqlColumnDefAst>? newColumns,
        IReadOnlyList<SqlForeignKeyAst>? foreignKeys)
    {
        var existing = DeserializeColumns(tableNode.ColumnsJson);
        var byName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < existing.Count; i++)
        {
            byName[existing[i].Name] = i;
        }

        if (newColumns is { Count: > 0 })
        {
            foreach (var col in newColumns)
            {
                if (string.IsNullOrWhiteSpace(col.Name)) continue;
                var fkStr = FormatFk(col.ForeignKey);
                var dto = new ColumnSchemaDto(
                    col.Name,
                    col.DataType,
                    col.IsPrimaryKey ? true : null,
                    col.IsNullable ? null : false,
                    col.IsUnique ? true : null,
                    fkStr);

                if (byName.TryGetValue(col.Name, out var idx))
                {
                    existing[idx] = dto;
                }
                else
                {
                    byName[col.Name] = existing.Count;
                    existing.Add(dto);
                }
            }
        }

        if (foreignKeys is { Count: > 0 })
        {
            foreach (var fk in foreignKeys)
            {
                if (string.IsNullOrWhiteSpace(fk.FromColumn)) continue;
                var fkStr = FormatFk(fk);
                if (string.IsNullOrEmpty(fkStr)) continue;
                if (byName.TryGetValue(fk.FromColumn, out var idx) && string.IsNullOrEmpty(existing[idx].ForeignKey))
                {
                    existing[idx] = existing[idx] with { ForeignKey = fkStr };
                }
            }
        }

        if (existing.Count > 0)
        {
            tableNode.ColumnsJson = JsonSerializer.Serialize(existing, ColumnJsonOptions);
        }
    }

    public static string? FormatFk(SqlForeignKeyAst? fk)
    {
        if (fk == null || string.IsNullOrWhiteSpace(fk.TargetTable.TableName))
        {
            return null;
        }
        return !string.IsNullOrWhiteSpace(fk.ToColumn)
            ? $"{fk.TargetTable.TableName}.{fk.ToColumn}"
            : fk.TargetTable.TableName;
    }

    public static void CollectStatementTableAccess(
        SqlStatementAst stmt,
        List<SqlTableRefAst> readTables,
        List<SqlTableRefAst> writeTables,
        List<SqlProcRefAst> calledProcs)
    {
        switch (stmt)
        {
            case SqlSelectStatementAst sel:
                readTables.AddRange(sel.Tables);
                readTables.AddRange(sel.Joins);
                break;
            case SqlInsertStatementAst ins:
                writeTables.Add(ins.TargetTable);
                if (ins.SourceQuery != null)
                {
                    CollectStatementTableAccess(ins.SourceQuery, readTables, writeTables, calledProcs);
                }
                break;
            case SqlUpdateStatementAst upd:
                writeTables.Add(upd.TargetTable);
                readTables.AddRange(upd.FromTables);
                break;
            case SqlDeleteStatementAst del:
                writeTables.Add(del.TargetTable);
                readTables.AddRange(del.UsingTables);
                break;
            case SqlMergeStatementAst mrg:
                writeTables.Add(mrg.TargetTable);
                if (mrg.UsingTable != null) readTables.Add(mrg.UsingTable);
                break;
            case SqlExecStatementAst ex:
                calledProcs.Add(ex.Procedure);
                break;
            case SqlCreateTableStatementAst ct:
                writeTables.Add(ct.Table);
                if (ct.AsSelectQuery != null)
                {
                    CollectStatementTableAccess(ct.AsSelectQuery, readTables, writeTables, calledProcs);
                }
                break;
        }

        foreach (var cte in stmt.Ctes)
        {
            if (cte.InnerStatement != null)
            {
                CollectStatementTableAccess(cte.InnerStatement, readTables, writeTables, calledProcs);
            }
        }
    }

    private static List<ColumnSchemaDto> DeserializeColumns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<ColumnSchemaDto>>(json, ColumnJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            // Ignore malformed legacy column payload and start fresh.
            return [];
        }
    }
}
