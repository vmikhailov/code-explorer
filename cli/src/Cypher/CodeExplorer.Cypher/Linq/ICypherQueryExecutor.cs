using System.Buffers.Binary;
using System.Data.Common;
using System.Runtime.CompilerServices;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Linq.Entities;

namespace CodeExplorer.Cypher.Linq;

public interface ICypherQueryExecutor
{
    Task<int> ExecuteCountAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default);
    Task<bool> ExecuteAnyAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default);
    Task<List<TElement>> ExecuteListAsync<TElement>(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default);
    Task<TElement?> ExecuteFirstOrDefaultAsync<TElement>(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default);
}

public static class DbEntityMaterializer
{
    public static async Task<int> ExecuteScalarCountAsync(
        DbConnection connection,
        CypherQuery query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int commandTimeout = 30,
        CancellationToken ct = default)
    {
        var compiled = SqliteCompiler.Compile(query, parameters);
        await using var cmd = CreateCommand(connection, compiled, commandTimeout);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar switch
        {
            null or DBNull => 0,
            int i => i,
            long l => (int)l,
            _ => Convert.ToInt32(scalar)
        };
    }

    public static async Task<bool> ExecuteScalarAnyAsync(
        DbConnection connection,
        CypherQuery query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int commandTimeout = 30,
        CancellationToken ct = default)
    {
        var compiled = SqliteCompiler.Compile(query, parameters);
        await using var cmd = CreateCommand(connection, compiled, commandTimeout);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar switch
        {
            null or DBNull => false,
            bool b => b,
            long l => l != 0,
            int i => i != 0,
            _ => Convert.ToInt64(scalar) != 0
        };
    }

    public static async Task<List<TElement>> ExecuteReaderListAsync<TElement>(
        DbConnection connection,
        CypherQuery query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int commandTimeout = 30,
        CancellationToken ct = default)
    {
        var compiled = SqliteCompiler.Compile(query, parameters, rawNodeColumns: true);
        await using var cmd = CreateCommand(connection, compiled, commandTimeout);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var createEntity = EntityFactory<TElement>.CreateGraphEntity;
        var list = new List<TElement>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(MaterializeRow<TElement>(reader, createEntity));
        }
        return list;
    }

    public static async Task<TElement?> ExecuteReaderFirstOrDefaultAsync<TElement>(
        DbConnection connection,
        CypherQuery query,
        IReadOnlyDictionary<string, object?>? parameters = null,
        int commandTimeout = 30,
        CancellationToken ct = default)
    {
        var compiled = SqliteCompiler.Compile(query, parameters, rawNodeColumns: true);
        await using var cmd = CreateCommand(connection, compiled, commandTimeout);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
        {
            return MaterializeRow<TElement>(reader, EntityFactory<TElement>.CreateGraphEntity);
        }
        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TElement MaterializeRow<TElement>(DbDataReader reader, Func<GraphEntity>? createEntity)
    {
        if (createEntity is not null)
        {
            var entity = createEntity();
            entity.Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            if (reader.FieldCount > 1 && !reader.IsDBNull(1))
            {
                var bytes = reader.GetFieldValue<byte[]>(1);
                if (bytes.Length > 0)
                {
                    SqliteJsonbReader.PopulateEntity(entity, bytes);
                }
            }
            return (TElement)(object)entity;
        }

        return EntityFactory<TElement>.Create();
    }

    private static DbCommand CreateCommand(DbConnection connection, SqliteCompiledQuery compiled, int commandTimeout)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandTimeout = commandTimeout;
        cmd.CommandText = compiled.Sql;

        foreach (var (k, v) in compiled.Parameters)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = "@" + k;
            param.Value = v ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }

        return cmd;
    }
}

/// <summary>
/// Direct binary reader for SQLite 3.45+ JSONB format (O(1) length-prefixed token navigation, zero System.Text.Json).
/// </summary>
public static class SqliteJsonbReader
{
    public const int TypeNull = 0;
    public const int TypeObject = 12;

    public static void PopulateEntity(GraphEntity entity, ReadOnlySpan<byte> jsonb)
    {
        var cursor = 0;
        if (!TryReadElement(jsonb, ref cursor, out var rootType, out var objPayload) || rootType != TypeObject)
        {
            return;
        }

        var objCursor = 0;
        while (objCursor < objPayload.Length)
        {
            if (!TryReadElement(objPayload, ref objCursor, out _, out var keySpan)) break;
            if (!TryReadElement(objPayload, ref objCursor, out var valType, out var valSpan)) break;

            if (valType != TypeNull)
            {
                entity.TryReadJsonbProperty(keySpan, valType, valSpan);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryReadElement(
        ReadOnlySpan<byte> buffer,
        ref int cursor,
        out int elementType,
        out ReadOnlySpan<byte> payload)
    {
        if ((uint)cursor >= (uint)buffer.Length)
        {
            elementType = 0;
            payload = default;
            return false;
        }

        var b0 = buffer[cursor];
        elementType = b0 & 0x0F;
        var sizeCode = b0 >> 4;

        int headerLen;
        int payloadLen;

        if (sizeCode <= 11)
        {
            headerLen = 1;
            payloadLen = sizeCode;
        }
        else if (sizeCode == 12)
        {
            headerLen = 2;
            payloadLen = buffer[cursor + 1];
        }
        else if (sizeCode == 13)
        {
            headerLen = 3;
            payloadLen = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(cursor + 1, 2));
        }
        else if (sizeCode == 14)
        {
            headerLen = 5;
            payloadLen = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(cursor + 1, 4));
        }
        else
        {
            headerLen = 9;
            payloadLen = (int)BinaryPrimitives.ReadUInt64BigEndian(buffer.Slice(cursor + 1, 8));
        }

        var payloadStart = cursor + headerLen;
        payload = buffer.Slice(payloadStart, payloadLen);
        cursor = payloadStart + payloadLen;
        return true;
    }
}

internal static class EntityFactory<T>
{
    public static readonly bool IsGraphEntity = typeof(GraphEntity).IsAssignableFrom(typeof(T));
    public static readonly Func<T> Create = CompileCreator();
    public static readonly Func<GraphEntity>? CreateGraphEntity = IsGraphEntity ? CompileGraphEntityCreator() : null;

    private static Func<T> CompileCreator()
    {
        var ctor = typeof(T).GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"Type '{typeof(T).Name}' does not have a parameterless constructor.");
        return System.Linq.Expressions.Expression.Lambda<Func<T>>(
            System.Linq.Expressions.Expression.New(ctor)).Compile();
    }

    private static Func<GraphEntity> CompileGraphEntityCreator()
    {
        var ctor = typeof(T).GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"Type '{typeof(T).Name}' does not have a parameterless constructor.");
        var newExpr = System.Linq.Expressions.Expression.New(ctor);
        var castExpr = System.Linq.Expressions.Expression.Convert(newExpr, typeof(GraphEntity));
        return System.Linq.Expressions.Expression.Lambda<Func<GraphEntity>>(castExpr).Compile();
    }
}
