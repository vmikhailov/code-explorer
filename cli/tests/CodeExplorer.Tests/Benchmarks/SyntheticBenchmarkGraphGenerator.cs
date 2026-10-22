using System.Diagnostics;
using System.Text;
using CodeExplorer.Core.Database;
using Microsoft.Data.Sqlite;

namespace CodeExplorer.Tests.Benchmarks;

public record BenchmarkGraphStats(
    int FileCount,
    int ClassCount,
    int MethodCount,
    int TableCount,
    int QueryCount,
    int TotalNodes,
    int TotalRelationships,
    long GenerationDurationMs);

public static class SyntheticBenchmarkGraphGenerator
{
    public const string WellKnownStartFunction = "Benchmark.Workflow.Step0";
    public const string WellKnownEndFunction = "Benchmark.Workflow.Step4";
    public const string WellKnownSearchSymbol = "PaymentProcessor_Benchmark";
    public const string WellKnownTable = "orders";
    public const string WellKnownQueriedBy = "ProcessOrdersAsync";
    public const string WellKnownCaller = "SubmitOrder";

    private sealed class NodeBatcher
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;
        private readonly StringBuilder _sb = new(64 * 1024);
        private int _batchCount;
        public int TotalAdded { get; private set; }

        public NodeBatcher(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
            _sb.Append("INSERT OR IGNORE INTO nodes (id, kind, properties) VALUES ");
        }

        public void Add(string id, string kind, string props)
        {
            if (_batchCount > 0) _sb.Append(',');
            _sb.Append("('");
            _sb.Append(id);
            _sb.Append("','");
            _sb.Append(kind);
            _sb.Append("','");
            _sb.Append(props.Replace("'", "''"));
            _sb.Append("')");
            _batchCount++;
            TotalAdded++;

            if (_batchCount >= 500)
            {
                Flush();
            }
        }

        public void Flush()
        {
            if (_batchCount == 0) return;
            _sb.Append(';');
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = _tx;
            cmd.CommandText = _sb.ToString();
            cmd.ExecuteNonQuery();

            _batchCount = 0;
            _sb.Clear();
            _sb.Append("INSERT OR IGNORE INTO nodes (id, kind, properties) VALUES ");
        }
    }

    private sealed class EdgeBatcher
    {
        private readonly SqliteConnection _conn;
        private readonly SqliteTransaction _tx;
        private readonly StringBuilder _sb = new(64 * 1024);
        private int _batchCount;
        public int TotalAdded { get; private set; }

        public EdgeBatcher(SqliteConnection conn, SqliteTransaction tx)
        {
            _conn = conn;
            _tx = tx;
            _sb.Append("INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties) VALUES ");
        }

        public void Add(string fromId, string toId, string kind)
        {
            if (_batchCount > 0) _sb.Append(',');
            _sb.Append("('");
            _sb.Append(fromId);
            _sb.Append("','");
            _sb.Append(toId);
            _sb.Append("','");
            _sb.Append(kind);
            _sb.Append("','{}')");
            _batchCount++;
            TotalAdded++;

            if (_batchCount >= 500)
            {
                Flush();
            }
        }

        public void Flush()
        {
            if (_batchCount == 0) return;
            _sb.Append(';');
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = _tx;
            cmd.CommandText = _sb.ToString();
            cmd.ExecuteNonQuery();

            _batchCount = 0;
            _sb.Clear();
            _sb.Append("INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties) VALUES ");
        }
    }

    public static async Task<BenchmarkGraphStats> GenerateAsync(
        SqliteGraphClient client,
        int fileCount = 10_000,
        int classCount = 25_000,
        int methodCount = 70_000,
        string workspaceId = "ws-bench",
        int seed = 42)
    {
        var sw = Stopwatch.StartNew();
        var conn = client.Connection;

        using (var pragmaCmd = conn.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA synchronous = OFF; PRAGMA journal_mode = OFF; PRAGMA temp_store = MEMORY; PRAGMA cache_size = -100000;";
            pragmaCmd.ExecuteNonQuery();
        }

        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();

        var nodeBatcher = new NodeBatcher(conn, tx);
        var edgeBatcher = new EdgeBatcher(conn, tx);

        // 1. Workspace Node
        nodeBatcher.Add(workspaceId, "Workspace", "{\"name\":\"BenchmarkWorkspace\",\"path\":\"/repo/root\"}");

        // 2. File Nodes (10,000)
        var fileIds = new string[fileCount];

        for (int i = 0; i < fileCount; i++)
        {
            var p = i / 100;
            var m = i / 10;
            var path = $"src/Project_{p}/Module_{m}/File_{i}.cs";
            var id = $"{workspaceId}:file:{path}";
            fileIds[i] = id;

            nodeBatcher.Add(id, "File", $"{{\"name\":\"File_{i}.cs\",\"path\":\"{path}\",\"file_path\":\"{path}\"}}");
        }

        // 3. Type Nodes (25,000)
        var classIds = new string[classCount];
        var classSymbols = new string[classCount];

        for (int i = 0; i < classCount; i++)
        {
            var fileIdx = i % fileCount;
            var filePath = $"src/Project_{fileIdx / 100}/Module_{fileIdx / 10}/File_{fileIdx}.cs";
            var isInterface = i % 10 == 0;
            var name = isInterface ? $"IService_{i}" : $"Service_{i}";
            var symbol = $"Benchmark.Services.{name}";
            var id = $"{workspaceId}:type:{filePath}:{name}";
            classIds[i] = id;
            classSymbols[i] = symbol;

            nodeBatcher.Add(id, "Type",
                $"{{\"name\":\"{name}\",\"symbol\":\"{symbol}\",\"kind\":\"{(isInterface ? "interface" : "class")}\",\"file_path\":\"{filePath}\",\"start_line\":10,\"end_line\":200}}");

            // (Type)-[:DECLARED_IN]->(File)
            edgeBatcher.Add(id, fileIds[fileIdx], "DECLARED_IN");

            // (Class)-[:IMPLEMENTS]->(Interface)
            if (!isInterface && i >= 10 && i % 5 == 0)
            {
                var targetInterfaceIdx = (i / 10) * 10;
                edgeBatcher.Add(id, classIds[targetInterfaceIdx], "IMPLEMENTS");
            }
        }

        // 4. Special Benchmark Nodes
        // 4a. find_symbol Target (50 methods matching WellKnownSearchSymbol to satisfy LIMIT 50 early)
        for (int m = 0; m < 50; m++)
        {
            var mName = m == 0 ? WellKnownSearchSymbol : $"{WellKnownSearchSymbol}_{m}";
            var searchSymId = $"{workspaceId}:func:special:{mName}";

            nodeBatcher.Add(searchSymId, "Function",
                $"{{\"name\":\"{mName}\",\"symbol\":\"Benchmark.Payments.{mName}\",\"file_path\":\"src/Payments/Processor.cs\",\"start_line\":{40 + m * 10},\"end_line\":{48 + m * 10}}}");
        }

        // 4b. get_call_chain 5-Hop Workflow Nodes
        var stepSymbols = new[]
        {
            WellKnownStartFunction, "Benchmark.Workflow.Step1", "Benchmark.Workflow.Step2",
            "Benchmark.Workflow.Step3", WellKnownEndFunction
        };
        var stepIds = new string[5];

        for (int i = 0; i < 5; i++)
        {
            var id = $"{workspaceId}:func:workflow:Step{i}";
            stepIds[i] = id;

            nodeBatcher.Add(id, "Function",
                $"{{\"name\":\"Step{i}\",\"symbol\":\"{stepSymbols[i]}\",\"file_path\":\"src/Workflow/Pipeline.cs\",\"start_line\":{10 + i * 20},\"end_line\":{25 + i * 20}}}");

            if (i > 0)
            {
                edgeBatcher.Add(stepIds[i - 1], id, "CALLS");
            }
        }

        // 4c. Data Lineage Benchmark Target ("orders" Table and surroundings)
        var ordersTableId = $"{workspaceId}:table:{WellKnownTable}";
        nodeBatcher.Add(ordersTableId, "Table", $"{{\"name\":\"{WellKnownTable}\"}}");

        var ordersQueryId = $"{workspaceId}:query:orders_select";
        nodeBatcher.Add(ordersQueryId, "Query",
            $"{{\"name\":\"SELECT Query: SELECT * FROM {WellKnownTable}\",\"query_text\":\"SELECT * FROM {WellKnownTable}\",\"path\":\"src/Repositories/OrderRepo.cs\"}}");
        edgeBatcher.Add(ordersQueryId, ordersTableId, "DEPENDS_ON");

        var queriedByFuncId = $"{workspaceId}:func:orders:{WellKnownQueriedBy}";
        nodeBatcher.Add(queriedByFuncId, "Function",
            $"{{\"name\":\"{WellKnownQueriedBy}\",\"symbol\":\"Benchmark.Orders.{WellKnownQueriedBy}\",\"file_path\":\"src/Repositories/OrderRepo.cs\",\"start_line\":50,\"end_line\":70}}");
        edgeBatcher.Add(ordersTableId, queriedByFuncId, "QUERIED_BY");

        var callerFuncId = $"{workspaceId}:func:orders:{WellKnownCaller}";
        nodeBatcher.Add(callerFuncId, "Function",
            $"{{\"name\":\"{WellKnownCaller}\",\"symbol\":\"Benchmark.Controllers.{WellKnownCaller}\",\"file_path\":\"src/Controllers/OrderController.cs\",\"start_line\":30,\"end_line\":45}}");
        edgeBatcher.Add(callerFuncId, queriedByFuncId, "CALLS");

        var orderEntityId = $"{workspaceId}:type:entities:OrderEntity";
        nodeBatcher.Add(orderEntityId, "Type",
            "{\"name\":\"OrderEntity\",\"symbol\":\"Benchmark.Entities.OrderEntity\",\"kind\":\"class\",\"file_path\":\"src/Entities/OrderEntity.cs\"}");
        edgeBatcher.Add(ordersTableId, orderEntityId, "IMPLEMENTED_BY");

        // 5. Function Nodes (70,000)
        var methodIds = new string[methodCount];

        for (int i = 0; i < methodCount; i++)
        {
            var classIdx = i % classCount;
            var fileIdx = classIdx % fileCount;
            var filePath = $"src/Project_{fileIdx / 100}/Module_{fileIdx / 10}/File_{fileIdx}.cs";
            var name = $"Method_{i}";
            var symbol = $"{classSymbols[classIdx]}.{name}";
            var id = $"{workspaceId}:func:{filePath}:{name}";
            methodIds[i] = id;

            nodeBatcher.Add(id, "Function",
                $"{{\"name\":\"{name}\",\"symbol\":\"{symbol}\",\"file_path\":\"{filePath}\",\"start_line\":20,\"end_line\":40}}");

            // (Function)-[:DECLARED_IN]->(File)
            edgeBatcher.Add(id, fileIds[fileIdx], "DECLARED_IN");

            // (Type)-[:HAS_METHOD]->(Function)
            edgeBatcher.Add(classIds[classIdx], id, "HAS_METHOD");
        }

        // 6. Additional Tables & Queries (99 more)
        const int additionalTables = 99;

        for (int t = 1; t <= additionalTables; t++)
        {
            var tId = $"{workspaceId}:table:table_{t}";
            nodeBatcher.Add(tId, "Table", $"{{\"name\":\"table_{t}\"}}");

            var qId = $"{workspaceId}:query:q_{t}";
            nodeBatcher.Add(qId, "Query",
                $"{{\"name\":\"SELECT Query: SELECT * FROM table_{t}\",\"query_text\":\"SELECT * FROM table_{t}\",\"path\":\"src/Db_{t}.cs\"}}");
            edgeBatcher.Add(qId, tId, "DEPENDS_ON");

            // Table -> Function QUERIED_BY link
            var fIdx = t * 40;

            if (fIdx < methodCount)
            {
                edgeBatcher.Add(tId, methodIds[fIdx], "QUERIED_BY");
            }
        }

        // 7. Pseudo-Random Realistic Method Calls to bring total relationships to exactly 300k
        var rng = new Random(seed);
        const int targetTotalRelationships = 300_000;
        var seenCalls = new HashSet<long>();

        while (edgeBatcher.TotalAdded < targetTotalRelationships)
        {
            var fromIdx = rng.Next(methodCount);
            var toIdx = (fromIdx + rng.Next(1, 100)) % methodCount;
            var key = ((long)fromIdx << 32) | (uint)toIdx;

            if (seenCalls.Add(key))
            {
                edgeBatcher.Add(methodIds[fromIdx], methodIds[toIdx], "CALLS");
            }
        }

        nodeBatcher.Flush();
        edgeBatcher.Flush();

        await tx.CommitAsync();
        sw.Stop();

        return new BenchmarkGraphStats(FileCount: fileCount, ClassCount: classCount,
            MethodCount: methodCount, TableCount: 100, QueryCount: 100, TotalNodes: nodeBatcher.TotalAdded,
            TotalRelationships: edgeBatcher.TotalAdded, GenerationDurationMs: sw.ElapsedMilliseconds);
    }
}
