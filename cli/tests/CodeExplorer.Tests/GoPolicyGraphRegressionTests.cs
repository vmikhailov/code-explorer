using CodeExplorer.Core.Diagrams;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class GoPolicyGraphRegressionTests
{
    [SetUp]
    public void SetUp()
    {
        WorkspaceIndexer.AutoRegisterParsers();
    }

    [Test]
    public async Task GoStandardLayoutWithCmdMain_ServeMux_PgxAndSqlite_ResolvesContractsLineageAndDeadCode()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_policygraph_repro_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var serverDir = Path.Combine(tempWorkspace, "server");
            var cmdDir = Path.Combine(serverDir, "cmd", "policy-qa");
            var configDir = Path.Combine(serverDir, "internal", "config");
            var authDir = Path.Combine(serverDir, "internal", "auth");
            var eggsDir = Path.Combine(serverDir, "internal", "eastereggs");
            var geminiDir = Path.Combine(serverDir, "internal", "gemini");
            var cypherDir = Path.Combine(serverDir, "internal", "cypher");
            var storeDir = Path.Combine(serverDir, "internal", "store");
            var httpApiDir = Path.Combine(serverDir, "internal", "httpapi");

            foreach (var d in new[] { cmdDir, configDir, authDir, eggsDir, geminiDir, cypherDir, storeDir, httpApiDir })
            {
                Directory.CreateDirectory(d);
            }

            await File.WriteAllTextAsync(Path.Combine(serverDir, "go.mod"), """
                module policygraph

                go 1.22

                require (
                    github.com/jackc/pgx/v5 v5.6.0
                    modernc.org/sqlite v1.30.1
                )
                """);

            await File.WriteAllTextAsync(Path.Combine(configDir, "config.go"), """
                package config

                type Config struct {
                    PostgresDSN string
                    SqlitePath  string
                }

                func Load() Config {
                    return Config{PostgresDSN: "postgres://localhost/pg", SqlitePath: "qa.db"}
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(authDir, "auth.go"), """
                package auth

                import "net/http"

                type Identity struct {
                    Email string
                }

                type Manager struct {
                    secret string
                }

                func NewManager(secret string) *Manager {
                    return &Manager{secret: secret}
                }

                func (m *Manager) CurrentUser(r *http.Request) (*Identity, error) {
                    return &Identity{Email: "user@example.com"}, nil
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(eggsDir, "eggs.go"), """
                package eastereggs

                type EggMeta struct {
                    Key   string
                    Title string
                }

                func All() []EggMeta {
                    return []EggMeta{{Key: "kermit", Title: "Kermit"}}
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(geminiDir, "client.go"), """
                package gemini

                type Client struct {
                    model string
                }

                func NewClient(model string) *Client {
                    return &Client{model: model}
                }

                func (c *Client) Summarize(prompt string) string {
                    return prompt
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(cypherDir, "pg.go"), """
                package cypher

                import (
                    "context"
                    "github.com/jackc/pgx/v5/pgxpool"
                )

                type Retriever struct {
                    pool *pgxpool.Pool
                }

                func NewRetriever(pool *pgxpool.Pool) *Retriever {
                    return &Retriever{pool: pool}
                }

                func (r *Retriever) QueryNodes(ctx context.Context) error {
                    _, err := r.pool.Query(ctx, "SELECT id, label FROM nodes WHERE active = true")
                    return err
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(storeDir, "store.go"), """
                package store

                import (
                    "database/sql"
                    _ "modernc.org/sqlite"
                )

                type Store struct {
                    db *sql.DB
                }

                func NewStore(path string) (*Store, error) {
                    db, err := sql.Open("sqlite", path)
                    if err != nil {
                        return nil, err
                    }
                    st := &Store{db: db}
                    if err := st.initSchema(); err != nil {
                        return nil, err
                    }
                    return st, nil
                }

                func (s *Store) initSchema() error {
                    _, err := s.db.Exec("CREATE TABLE IF NOT EXISTS qa_entries (id TEXT PRIMARY KEY, question TEXT)")
                    if err != nil {
                        return err
                    }
                    _, err = s.db.Exec("CREATE TABLE IF NOT EXISTS easter_egg_settings (key TEXT PRIMARY KEY, enabled INTEGER)")
                    return err
                }

                func (s *Store) SaveHistory(question string) error {
                    _, err := s.db.Exec("INSERT INTO qa_entries (id, question) VALUES ('1', ?)", question)
                    return err
                }

                func UnusedLegacyTopLevel() {}
                """);

            await File.WriteAllTextAsync(Path.Combine(httpApiDir, "server.go"), """
                package httpapi

                import (
                    "net/http"
                    "policygraph/internal/auth"
                    "policygraph/internal/cypher"
                    "policygraph/internal/eastereggs"
                    "policygraph/internal/gemini"
                    "policygraph/internal/store"
                )

                type Server struct {
                    authMgr   *auth.Manager
                    store     *store.Store
                    retriever *cypher.Retriever
                    llm       *gemini.Client
                }

                func NewServer(authMgr *auth.Manager, st *store.Store, ret *cypher.Retriever, llm *gemini.Client) *http.ServeMux {
                    s := &Server{
                        authMgr:   authMgr,
                        store:     st,
                        retriever: ret,
                        llm:       llm,
                    }
                    mux := http.NewServeMux()
                    mux.HandleFunc("POST /api/auth/logout", s.handleLogout)
                    mux.HandleFunc("POST /api/ask/stream", s.withAuth(s.handleAsk))
                    mux.HandleFunc("GET /api/eggs", s.handleEggs)
                    return mux
                }

                func (s *Server) withAuth(next http.HandlerFunc) http.HandlerFunc {
                    return func(w http.ResponseWriter, r *http.Request) {
                        _, _ = s.authMgr.CurrentUser(r)
                        next(w, r)
                    }
                }

                func (s *Server) handleLogout(w http.ResponseWriter, r *http.Request) {
                    http.SetCookie(w, &http.Cookie{Name: "session", Value: "", MaxAge: -1})
                }

                func (s *Server) handleAsk(w http.ResponseWriter, r *http.Request) {
                    _ = s.retriever.QueryNodes(r.Context())
                    _ = s.llm.Summarize("q")
                    _ = s.store.SaveHistory("q")
                }

                func (s *Server) handleEggs(w http.ResponseWriter, r *http.Request) {
                    _ = eastereggs.All()
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(cmdDir, "main.go"), """
                package main

                import (
                    "context"
                    "net/http"
                    "github.com/jackc/pgx/v5/pgxpool"
                    "policygraph/internal/auth"
                    "policygraph/internal/config"
                    "policygraph/internal/cypher"
                    "policygraph/internal/gemini"
                    "policygraph/internal/httpapi"
                    "policygraph/internal/store"
                )

                func main() {
                    cfg := config.Load()
                    pool, _ := pgxpool.New(context.Background(), cfg.PostgresDSN)
                    st, _ := store.NewStore(cfg.SqlitePath)
                    mgr := auth.NewManager("secret")
                    ret := cypher.NewRetriever(pool)
                    llm := gemini.NewClient("gemini-2.5-flash")
                    mux := httpapi.NewServer(mgr, st, ret, llm)
                    _ = http.ListenAndServe(":8080", mux)
                }
                """);

            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);

            var indexer = new WorkspaceIndexer(db);
            await indexer.IndexAsync(tempWorkspace, clear: true);

            // Bug #1A: Single-binary Go module (`server/go.mod` + `server/cmd/policy-qa/main.go`) stays 1 unified Project (`server`)
            var projectNames = await db.QueryAsync<string>("MATCH (p:Project) RETURN p.name AS name");
            projectNames.Should().ContainSingle(n => n == "server");
            projectNames.Should().NotContain("policy-qa");

            // Bug #1B: Live cross-package exported symbols & types are NOT falsely flagged as dead_code, while genuine dead code IS flagged
            var deadRes = await db.ExecuteCypherAsync(Queries.Get("find_refactor_dead_code"), new { projectName = "server" });
            var deadNames = deadRes.Rows.Select(r => r.GetStringProp("name")).ToList();

            deadNames.Should().NotContain("NewManager");
            deadNames.Should().NotContain("Manager");
            deadNames.Should().NotContain("Identity");
            deadNames.Should().NotContain("Config");
            deadNames.Should().NotContain("Load");
            deadNames.Should().NotContain("Retriever");
            deadNames.Should().NotContain("NewRetriever");
            deadNames.Should().NotContain("Client");
            deadNames.Should().NotContain("NewClient");
            deadNames.Should().NotContain("EggMeta");
            deadNames.Should().NotContain("All");
            deadNames.Should().NotContain("NewServer");
            deadNames.Should().NotContain("handleLogout");
            deadNames.Should().NotContain("handleAsk");
            deadNames.Should().NotContain("handleEggs");
            deadNames.Should().NotContain("main");
            deadNames.Should().Contain("UnusedLegacyTopLevel");

            // Bug #2: get_service_contracts("server") returns all ingress endpoints, net/http framework, and BOTH SQLite & PostgreSQL
            var viewEngine = new ArchitectureViewEngine(db);
            var contract = await viewEngine.GetServiceContractsAsync("server");

            contract.Framework.Should().Be("net/http");
            contract.IngressEndpoints.Should().Contain("POST:/api/auth/logout");
            contract.IngressEndpoints.Should().Contain("POST:/api/ask/stream");
            contract.IngressEndpoints.Should().Contain("GET:/api/eggs");
            contract.Databases.Should().Contain(d => d.Contains("SQLite", StringComparison.OrdinalIgnoreCase));
            contract.Databases.Should().Contain(d => d.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase));

            // Bug #3: C3 view does NOT over-approximate POST:/api/auth/logout to CREATE TABLE DDL queries in store.go
            var c3 = await viewEngine.GetComponentViewAsync("server");
            var logoutEp = c3.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Endpoint && n.Name == "POST:/api/auth/logout");
            var askEp = c3.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Endpoint && n.Name == "POST:/api/ask/stream");
            logoutEp.Should().NotBeNull();
            askEp.Should().NotBeNull();

            var logoutTargets = c3.Edges
                .Where(e => e.Source == logoutEp!.Id)
                .Select(e => c3.Nodes.FirstOrDefault(n => n.Id == e.Target))
                .OfType<GraphNodeDto>()
                .ToList();
            logoutTargets.Should().NotContain(n => n.Kind == NodeKind.Query || n.Kind == NodeKind.Table,
                "POST /api/auth/logout only clears a cookie and must not link to store.go DDL/DML queries");

            var askTargets = c3.Edges
                .Where(e => e.Source == askEp!.Id)
                .Select(e => c3.Nodes.FirstOrDefault(n => n.Id == e.Target))
                .OfType<GraphNodeDto>()
                .ToList();
            askTargets.Should().NotContain(n => n.Kind == NodeKind.Query && n.Name.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase),
                "POST /api/ask/stream must not inherit CREATE TABLE schema init queries from NewStore");
            askTargets.Should().Contain(n =>
                (n.Kind == NodeKind.Query && (n.Name.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) || n.Name.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))) ||
                (n.Kind == NodeKind.Table && (n.Name == "qa_entries" || n.Name == "nodes")));

            // Bug #4: Raw SQL data lineage diagram (`view: "lineage"`) renders non-empty accessors and tables
            var lineageMermaid = await DiagramExporter.ExportAsync(db, format: "mermaid", type: "lineage", projectFilter: "server");
            lineageMermaid.Should().Contain("qa_entries");
            lineageMermaid.Should().Contain("nodes");
            lineageMermaid.Should().Contain("SaveHistory");
            lineageMermaid.Should().Contain("QueryNodes");
            lineageMermaid.Should().Contain("-->|WRITES_TO|");
            lineageMermaid.Should().Contain("-->|READS_FROM|");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                try
                {
                    Directory.Delete(tempWorkspace, true);
                }
                catch (IOException)
                {
                    // SQLite pool cleanup on Windows may release file handle asynchronously
                }
            }
        }
    }

    [Test]
    public async Task GoMultiBinaryLayoutWithMultipleCmdTargets_PreservesSubprojectsAndLinksSharedInternalCalls()
    {
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_go_multibin_" + Guid.NewGuid()).Replace('\\', '/');
        Directory.CreateDirectory(tempWorkspace);

        try
        {
            var apiDir = Path.Combine(tempWorkspace, "cmd", "api-server");
            var workerDir = Path.Combine(tempWorkspace, "cmd", "bg-worker");
            var sharedDir = Path.Combine(tempWorkspace, "internal", "shared");
            foreach (var d in new[] { apiDir, workerDir, sharedDir })
            {
                Directory.CreateDirectory(d);
            }

            await File.WriteAllTextAsync(Path.Combine(tempWorkspace, "go.mod"), "module example.com/monogo\n\ngo 1.22\n");
            await File.WriteAllTextAsync(Path.Combine(sharedDir, "shared.go"), """
                package shared

                type Dispatcher struct{}

                func NewDispatcher() *Dispatcher {
                    return &Dispatcher{}
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(apiDir, "main.go"), """
                package main

                import "example.com/monogo/internal/shared"

                func main() {
                    _ = shared.NewDispatcher()
                }
                """);
            await File.WriteAllTextAsync(Path.Combine(workerDir, "main.go"), """
                package main

                import "example.com/monogo/internal/shared"

                func main() {
                    _ = shared.NewDispatcher()
                }
                """);

            var dbPath = Path.Combine(tempWorkspace, "graph.db").Replace('\\', '/');
            using var db = new SqliteGraphClient(dbPath);
            await new WorkspaceIndexer(db).IndexAsync(tempWorkspace, clear: true);

            var projectNames = await db.QueryAsync<string>("MATCH (p:Project) RETURN p.name AS name");
            projectNames.Should().Contain("api-server");
            projectNames.Should().Contain("bg-worker");

            var deadRes = await db.ExecuteCypherAsync(Queries.Get("find_refactor_dead_code"), new { projectName = "" });
            var deadNames = deadRes.Rows.Select(r => r.GetStringProp("name")).ToList();
            deadNames.Should().NotContain("NewDispatcher");
            deadNames.Should().NotContain("Dispatcher");
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                try
                {
                    Directory.Delete(tempWorkspace, true);
                }
                catch (IOException)
                {
                    // SQLite pool cleanup on Windows may release file handle asynchronously
                }
            }
        }
    }
}
