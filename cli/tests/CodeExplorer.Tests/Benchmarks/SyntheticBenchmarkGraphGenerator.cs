using System.Diagnostics;
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

        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync();

        using var cmdNode = conn.CreateCommand();

        cmdNode.Transaction = tx;
        cmdNode.CommandText = "INSERT OR IGNORE INTO nodes (id, kind, properties) VALUES (@id, @kind, @props);";
        var pId = cmdNode.Parameters.Add("@id", SqliteType.Text);
        var pKind = cmdNode.Parameters.Add("@kind", SqliteType.Text);
        var pProps = cmdNode.Parameters.Add("@props", SqliteType.Text);

        using var cmdEdge = conn.CreateCommand();

        cmdEdge.Transaction = tx;

        cmdEdge.CommandText =
            "INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties) VALUES (@from, @to, @kind, '{}');";
        var pFrom = cmdEdge.Parameters.Add("@from", SqliteType.Text);
        var pTo = cmdEdge.Parameters.Add("@to", SqliteType.Text);
        var pKindE = cmdEdge.Parameters.Add("@kind", SqliteType.Text);

        int totalNodes = 0;
        int totalRelationships = 0;

        // 1. Workspace Node
        pId.Value = workspaceId;
        pKind.Value = "Workspace";
        pProps.Value = "{\"name\":\"BenchmarkWorkspace\",\"path\":\"/repo/root\"}";
        cmdNode.ExecuteNonQuery();
        totalNodes++;

        // 2. File Nodes (10,000)
        var fileIds = new string[fileCount];

        for (int i = 0; i < fileCount; i++)
        {
            var p = i / 100;
            var m = i / 10;
            var path = $"src/Project_{p}/Module_{m}/File_{i}.cs";
            var id = $"{workspaceId}:file:{path}";
            fileIds[i] = id;

            pId.Value = id;
            pKind.Value = "File";
            pProps.Value = $"{{\"name\":\"File_{i}.cs\",\"path\":\"{path}\",\"file_path\":\"{path}\"}}";
            cmdNode.ExecuteNonQuery();
            totalNodes++;
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

            pId.Value = id;
            pKind.Value = "Type";

            pProps.Value =
                $"{{\"name\":\"{name}\",\"symbol\":\"{symbol}\",\"kind\":\"{(isInterface ? "interface" : "class")}\",\"file_path\":\"{filePath}\",\"start_line\":10,\"end_line\":200}}";
            cmdNode.ExecuteNonQuery();
            totalNodes++;

            // (Type)-[:DECLARED_IN]->(File)
            pFrom.Value = id;
            pTo.Value = fileIds[fileIdx];
            pKindE.Value = "DECLARED_IN";
            cmdEdge.ExecuteNonQuery();
            totalRelationships++;

            // (Class)-[:IMPLEMENTS]->(Interface)
            if (!isInterface && i >= 10 && i % 5 == 0)
            {
                var targetInterfaceIdx = (i / 10) * 10;
                pFrom.Value = id;
                pTo.Value = classIds[targetInterfaceIdx];
                pKindE.Value = "IMPLEMENTS";
                cmdEdge.ExecuteNonQuery();
                totalRelationships++;
            }
        }

        // 4. Special Benchmark Nodes
        // 4a. find_symbol Target (50 methods matching WellKnownSearchSymbol to satisfy LIMIT 50 early)
        for (int m = 0; m < 50; m++)
        {
            var mName = m == 0 ? WellKnownSearchSymbol : $"{WellKnownSearchSymbol}_{m}";
            var searchSymId = $"{workspaceId}:func:special:{mName}";
            pId.Value = searchSymId;
            pKind.Value = "Function";

            pProps.Value =
                $"{{\"name\":\"{mName}\",\"symbol\":\"Benchmark.Payments.{mName}\",\"file_path\":\"src/Payments/Processor.cs\",\"start_line\":{40 + m * 10},\"end_line\":{48 + m * 10}}}";
            cmdNode.ExecuteNonQuery();
            totalNodes++;
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
            pId.Value = id;
            pKind.Value = "Function";

            pProps.Value =
                $"{{\"name\":\"Step{i}\",\"symbol\":\"{stepSymbols[i]}\",\"file_path\":\"src/Workflow/Pipeline.cs\",\"start_line\":{10 + i * 20},\"end_line\":{25 + i * 20}}}";
            cmdNode.ExecuteNonQuery();
            totalNodes++;

            if (i > 0)
            {
                pFrom.Value = stepIds[i - 1];
                pTo.Value = id;
                pKindE.Value = "CALLS";
                cmdEdge.ExecuteNonQuery();
                totalRelationships++;
            }
        }

        // 4c. Data Lineage Benchmark Target ("orders" Table and surroundings)
        var ordersTableId = $"{workspaceId}:table:{WellKnownTable}";
        pId.Value = ordersTableId;
        pKind.Value = "Table";
        pProps.Value = $"{{\"name\":\"{WellKnownTable}\"}}";
        cmdNode.ExecuteNonQuery();
        totalNodes++;

        var ordersQueryId = $"{workspaceId}:query:orders_select";
        pId.Value = ordersQueryId;
        pKind.Value = "Query";

        pProps.Value =
            $"{{\"name\":\"SELECT Query: SELECT * FROM {WellKnownTable}\",\"query_text\":\"SELECT * FROM {WellKnownTable}\",\"path\":\"src/Repositories/OrderRepo.cs\"}}";
        cmdNode.ExecuteNonQuery();
        totalNodes++;

        pFrom.Value = ordersQueryId;
        pTo.Value = ordersTableId;
        pKindE.Value = "DEPENDS_ON";
        cmdEdge.ExecuteNonQuery();
        totalRelationships++;

        var queriedByFuncId = $"{workspaceId}:func:orders:{WellKnownQueriedBy}";
        pId.Value = queriedByFuncId;
        pKind.Value = "Function";

        pProps.Value =
            $"{{\"name\":\"{WellKnownQueriedBy}\",\"symbol\":\"Benchmark.Orders.{WellKnownQueriedBy}\",\"file_path\":\"src/Repositories/OrderRepo.cs\",\"start_line\":50,\"end_line\":70}}";
        cmdNode.ExecuteNonQuery();
        totalNodes++;

        pFrom.Value = ordersTableId;
        pTo.Value = queriedByFuncId;
        pKindE.Value = "QUERIED_BY";
        cmdEdge.ExecuteNonQuery();
        totalRelationships++;

        var callerFuncId = $"{workspaceId}:func:orders:{WellKnownCaller}";
        pId.Value = callerFuncId;
        pKind.Value = "Function";

        pProps.Value =
            $"{{\"name\":\"{WellKnownCaller}\",\"symbol\":\"Benchmark.Controllers.{WellKnownCaller}\",\"file_path\":\"src/Controllers/OrderController.cs\",\"start_line\":30,\"end_line\":45}}";
        cmdNode.ExecuteNonQuery();
        totalNodes++;

        pFrom.Value = callerFuncId;
        pTo.Value = queriedByFuncId;
        pKindE.Value = "CALLS";
        cmdEdge.ExecuteNonQuery();
        totalRelationships++;

        var orderEntityId = $"{workspaceId}:type:entities:OrderEntity";
        pId.Value = orderEntityId;
        pKind.Value = "Type";

        pProps.Value =
            "{\"name\":\"OrderEntity\",\"symbol\":\"Benchmark.Entities.OrderEntity\",\"kind\":\"class\",\"file_path\":\"src/Entities/OrderEntity.cs\"}";
        cmdNode.ExecuteNonQuery();
        totalNodes++;

        pFrom.Value = orderEntityId;
        pTo.Value = ordersTableId;
        pKindE.Value = "PERSISTED_IN";
        cmdEdge.ExecuteNonQuery();
        totalRelationships++;

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

            pId.Value = id;
            pKind.Value = "Function";

            pProps.Value =
                $"{{\"name\":\"{name}\",\"symbol\":\"{symbol}\",\"file_path\":\"{filePath}\",\"start_line\":20,\"end_line\":40}}";
            cmdNode.ExecuteNonQuery();
            totalNodes++;

            // (Function)-[:DECLARED_IN]->(File)
            pFrom.Value = id;
            pTo.Value = fileIds[fileIdx];
            pKindE.Value = "DECLARED_IN";
            cmdEdge.ExecuteNonQuery();
            totalRelationships++;

            // (Type)-[:HAS_METHOD]->(Function)
            pFrom.Value = classIds[classIdx];
            pTo.Value = id;
            pKindE.Value = "HAS_METHOD";
            cmdEdge.ExecuteNonQuery();
            totalRelationships++;
        }

        // 6. Additional Tables & Queries (99 more)
        const int additionalTables = 99;

        for (int t = 1; t <= additionalTables; t++)
        {
            var tId = $"{workspaceId}:table:table_{t}";
            pId.Value = tId;
            pKind.Value = "Table";
            pProps.Value = $"{{\"name\":\"table_{t}\"}}";
            cmdNode.ExecuteNonQuery();
            totalNodes++;

            var qId = $"{workspaceId}:query:q_{t}";
            pId.Value = qId;
            pKind.Value = "Query";

            pProps.Value =
                $"{{\"name\":\"SELECT Query: SELECT * FROM table_{t}\",\"query_text\":\"SELECT * FROM table_{t}\",\"path\":\"src/Db_{t}.cs\"}}";
            cmdNode.ExecuteNonQuery();
            totalNodes++;

            pFrom.Value = qId;
            pTo.Value = tId;
            pKindE.Value = "DEPENDS_ON";
            cmdEdge.ExecuteNonQuery();
            totalRelationships++;

            // Table -> Function QUERIED_BY link
            var fIdx = t * 40;

            if (fIdx < methodCount)
            {
                pFrom.Value = tId;
                pTo.Value = methodIds[fIdx];
                pKindE.Value = "QUERIED_BY";
                cmdEdge.ExecuteNonQuery();
                totalRelationships++;
            }
        }

        // 7. Pseudo-Random Realistic Method Calls to bring total relationships to exactly 300k
        var rng = new Random(seed);
        const int targetTotalRelationships = 300_000;

        while (totalRelationships < targetTotalRelationships)
        {
            var fromIdx = rng.Next(methodCount);
            var toIdx = (fromIdx + rng.Next(1, 100)) % methodCount;

            pFrom.Value = methodIds[fromIdx];
            pTo.Value = methodIds[toIdx];
            pKindE.Value = "CALLS";

            if (cmdEdge.ExecuteNonQuery() > 0)
            {
                totalRelationships++;
            }
        }

        await tx.CommitAsync();
        sw.Stop();

        return new BenchmarkGraphStats(FileCount: fileCount, ClassCount: classCount,
            MethodCount: methodCount, TableCount: 100, QueryCount: 100, TotalNodes: totalNodes,
            TotalRelationships: totalRelationships, GenerationDurationMs: sw.ElapsedMilliseconds);
    }
}
