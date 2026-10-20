using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class PothosAndApolloGraphQLTests
{
    private string _tempWorkspace = null!;
    private string _dbPath = null!;
    private SqliteGraphClient _client = null!;
    private CodeExplorerRepository _repo = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_pothos_apollo_test_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        Directory.CreateDirectory(_tempWorkspace);

        // 1. Backend project using Pothos code-first GraphQL with multiple SchemaBuilder instances
        var apiDir = Path.Combine(_tempWorkspace, "api").Replace('\\', '/');
        Directory.CreateDirectory(apiDir);
        await File.WriteAllTextAsync(
            Path.Combine(apiDir, "package.json"),
            """
            {
              "name": "backend-api",
              "dependencies": {
                "@pothos/core": "^3.0.0"
              }
            }
            """);

        var reposCode = """
            export class PublishedItemRepo {
              async findPublished(limit: number) {
                return [{ id: '1', title: 'Hello' }];
              }
            }

            export class AuditLogRepo {
              async list(filter: string) {
                return [{ id: 'a1', action: filter }];
              }
            }

            export class HealthService {
              checkStatus() {
                return 'OK';
              }
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(apiDir, "repos.ts"), reposCode);

        var schemaCode = """
            import SchemaBuilder from '@pothos/core';
            import { PublishedItemRepo, AuditLogRepo, HealthService } from './repos';

            export const clientBuilder = new SchemaBuilder<{ Context: any }>({});
            export const adminBuilder = new SchemaBuilder<{ Context: any }>({});
            export const monitoringBuilder = new SchemaBuilder<{ Context: any }>({});

            // Single root query on clientBuilder + variable assignment from generic DI resolve
            clientBuilder.queryField('publishedItems', (t) =>
              t.field({
                type: [PublishedItemRef],
                resolve: async (_root, args, ctx) => {
                  const repo = ctx.container.resolve<PublishedItemRepo>('publishedItemRepo');
                  return await repo.findPublished(args.limit);
                },
              })
            );

            // Single root query on adminBuilder + direct chained call on generic DI resolve
            adminBuilder.queryField('auditLogs', (t) =>
              t.field({
                type: AuditLogConnection,
                resolve: async (_root, args, ctx) => {
                  return ctx.container.resolve<AuditLogRepo>('auditLogRepo').list(args.filter);
                },
              })
            );

            // Batch root fields on monitoringBuilder
            monitoringBuilder.queryFields((t) => ({
              healthCheck: t.field({
                type: 'String',
                resolve: (_root, _args, ctx) => {
                  return ctx.container.resolve<HealthService>('healthService').checkStatus();
                },
              }),
            }));
            """;
        await File.WriteAllTextAsync(Path.Combine(apiDir, "schema.ts"), schemaCode);

        // 2. Frontend project using Apollo Client:
        //    - Variant B: .graphql operation file + React component calling generated hook name without indexing generated code
        //    - Variant A: indexed codegen TypedDocumentNode + wrapper hook + React component
        var webDir = Path.Combine(_tempWorkspace, "web").Replace('\\', '/');
        Directory.CreateDirectory(webDir);
        await File.WriteAllTextAsync(
            Path.Combine(webDir, "package.json"),
            """
            {
              "name": "web-client",
              "dependencies": {
                "@apollo/client": "^3.8.0",
                "react": "^18.2.0"
              }
            }
            """);

        // Variant B: Raw .graphql file (no codegen file needed for `PublishedItems`)
        var gqlOperationContent = """
            query PublishedItems($limit: Int) {
              items: publishedItems(limit: $limit) {
                id
                title
              }
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(webDir, "publishedItems.graphql"), gqlOperationContent);

        var publishedPageCode = """
            import { usePublishedItemsQuery } from './generated/graphql';

            export function PublishedItemsPage() {
              const { data } = usePublishedItemsQuery({ variables: { limit: 10 } });
              return data;
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(webDir, "PublishedItemsPage.tsx"), publishedPageCode);

        // Variant A: Indexed codegen file with TypedDocumentNode JSON AST + hook wrapper for `AuditLogs`
        var codegenFileContent = """
            import * as Apollo from '@apollo/client';

            export const AuditLogsDocument = {
              kind: 'Document',
              definitions: [
                {
                  kind: 'OperationDefinition',
                  operation: 'query',
                  name: { kind: 'Name', value: 'AuditLogs' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      {
                        kind: 'Field',
                        name: { kind: 'Name', value: 'auditLogs' },
                        selectionSet: {
                          kind: 'SelectionSet',
                          selections: [
                            { kind: 'Field', name: { kind: 'Name', value: 'id' } }
                          ]
                        }
                      }
                    ]
                  }
                }
              ]
            } as unknown as Apollo.DocumentNode;

            export function useAuditLogsQuery(baseOptions?: any) {
              return Apollo.useQuery(AuditLogsDocument, baseOptions);
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(webDir, "auditLogs.generated.ts"), codegenFileContent);

        var auditLogsTableCode = """
            import { useAuditLogsQuery } from './auditLogs.generated';

            export function AuditLogsTable() {
              const { data } = useAuditLogsQuery();
              return data;
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(webDir, "AuditLogsTable.tsx"), auditLogsTableCode);

        _dbPath = Path.Combine(_tempWorkspace, "test_graphql.db");
        _client = new SqliteGraphClient(_dbPath);

        WorkspaceIndexer.AutoRegisterParsers();
        var indexer = new WorkspaceIndexer(_client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        _repo = new CodeExplorerRepository(_client);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _client.DisposeAsync();
        if (Directory.Exists(_tempWorkspace))
        {
            try { Directory.Delete(_tempWorkspace, true); } catch { }
        }
    }

    [Test]
    public async Task Pothos_MultiBuilder_Extracts_GraphQL_Endpoints_And_Resolvers()
    {
        var entryPoints = await _repo.GetProjectEntryPointsAsync("backend-api");

        entryPoints.Should().Contain("publishedItems");
        entryPoints.Should().Contain("auditLogs");
        entryPoints.Should().Contain("healthCheck");
        entryPoints.Should().Contain("GraphQL");
        entryPoints.Should().Contain("PublishedItemRef");
        entryPoints.Should().Contain("AuditLogConnection");
        entryPoints.Should().Contain("publishedItems.resolve");
        entryPoints.Should().Contain("auditLogs.resolve");
        entryPoints.Should().Contain("healthCheck.resolve");
    }

    [Test]
    public async Task Pothos_Resolver_Traces_Downstream_Through_Generic_DI_Container()
    {
        // 1. Variable bound from generic DI call: `const repo = ctx.container.resolve<PublishedItemRepo>(...); repo.findPublished(...)`
        var publishedChain = await _repo.GetCallChainAsync("publishedItems", "findPublished", 4);
        publishedChain.Should().Contain("publishedItems.resolve");
        publishedChain.Should().Contain("findPublished");

        // 2. Direct chained call on generic DI call: `ctx.container.resolve<AuditLogRepo>(...).list(...)`
        var auditChain = await _repo.GetCallChainAsync("auditLogs", "list", 4);
        auditChain.Should().Contain("auditLogs.resolve");
        auditChain.Should().Contain("list");
    }

    [Test]
    public async Task VariantB_GraphQL_File_Bridges_Frontend_Component_To_Backend_Pothos_Resolver()
    {
        // End-to-end call chain from React component PublishedItemsPage -> usePublishedItemsQuery (.graphql) -> publishedItems.resolve -> findPublished
        var chain = await _repo.GetCallChainAsync("PublishedItemsPage", "findPublished", 5);

        chain.Should().Contain("PublishedItemsPage");
        chain.Should().Contain("usePublishedItemsQuery");
        chain.Should().Contain("publishedItems.resolve");
        chain.Should().Contain("findPublished");

        // Impact analysis on GraphQL field name `publishedItems`
        var impact = await _repo.AnalyzeCodeImpactAsync("publishedItems");
        impact.Should().Contain("usePublishedItemsQuery");
        impact.Should().Contain("PublishedItemsPage");
    }

    [Test]
    public async Task VariantA_Codegen_TypedDocumentNode_Bridges_Frontend_Component_To_Backend_Pothos_Resolver()
    {
        // End-to-end call chain from React component AuditLogsTable -> useAuditLogsQuery -> AuditLogsDocument -> auditLogs.resolve -> list
        var chain = await _repo.GetCallChainAsync("AuditLogsTable", "list", 5);

        chain.Should().Contain("AuditLogsTable");
        chain.Should().Contain("useAuditLogsQuery");
        chain.Should().Contain("AuditLogsDocument");
        chain.Should().Contain("auditLogs.resolve");
        chain.Should().Contain("list");

        // Impact analysis on GraphQL field name `auditLogs`
        var impact = await _repo.AnalyzeCodeImpactAsync("auditLogs");
        impact.Should().Contain("AuditLogsDocument");
    }
}
