using CodeExplorer.Core.Diagrams;
using CodeExplorer.Core.Protocol;
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

                import (
                    "errors"
                    "net/http"
                )

                type SessionStore interface {
                    RevokeSession(jti string) error
                }

                type Identity struct {
                    Email string
                }

                type Manager struct {
                    secret   string
                    sessions SessionStore
                }

                func NewManager(secret string, sessions SessionStore) *Manager {
                    return &Manager{secret: secret, sessions: sessions}
                }

                func (m *Manager) CurrentUser(r *http.Request) (*Identity, error) {
                    if m.secret == "" {
                        return nil, errors.New("missing secret")
                    }
                    return &Identity{Email: "user@example.com"}, nil
                }

                func (m *Manager) RevokeToken(jti string) error {
                    if jti == "" {
                        return errors.New("empty jti")
                    }
                    return m.sessions.RevokeSession(jti)
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(authDir, "oidc.go"), """
                package auth

                import (
                    "errors"
                    "net/http"
                    "net/url"
                )

                type OIDCProvider struct {
                    httpClient *http.Client
                }

                func NewOIDCProvider() *OIDCProvider {
                    return &OIDCProvider{httpClient: &http.Client{}}
                }

                func (p *OIDCProvider) CompleteAuth(u *url.URL) error {
                    code := u.Query().Get("code")
                    if code == "" {
                        return errors.New("missing code")
                    }
                    return nil
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(authDir, "oidc_test.go"), """
                package auth

                import (
                    "database/sql"
                    "testing"
                )

                func TestOIDCSmoke(t *testing.T) {
                    var db *sql.DB
                    if db != nil {
                        _, _ = db.Query("SELECT 1 FROM test_only_table")
                    }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(eggsDir, "eggs.go"), """
                package eastereggs

                import "policygraph/internal/store"

                type EggMeta struct {
                    Key   string
                    Title string
                }

                type Manager struct {
                    store *store.Store
                }

                func NewManager(st *store.Store) *Manager {
                    return &Manager{store: st}
                }

                func All() []EggMeta {
                    return []EggMeta{{Key: "kermit", Title: "Kermit"}}
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(geminiDir, "client.go"), """
                package gemini

                import "errors"

                type Client struct {
                    model string
                }

                func NewClient(model string) *Client {
                    return &Client{model: model}
                }

                func (c *Client) Summarize(prompt string) (string, error) {
                    if c.model == "" {
                        return "", errors.New("missing model")
                    }
                    return prompt, nil
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(cypherDir, "pg.go"), """
                package cypher

                import (
                    "context"
                    "github.com/jackc/pgx/v5/pgxpool"
                )

                type Client struct {
                    pool *pgxpool.Pool
                }

                type Retriever struct {
                    client *Client
                    pool   *pgxpool.Pool
                }

                func NewRetriever(pool *pgxpool.Pool) *Retriever {
                    return &Retriever{client: &Client{pool: pool}, pool: pool}
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

                func New(path string) (*Store, error) {
                    db, err := sql.Open("sqlite", path)
                    if err != nil {
                        return nil, err
                    }
                    st := &Store{db: db}
                    if err := st.initSQLite(); err != nil {
                        return nil, err
                    }
                    return st, nil
                }

                func (s *Store) initSQLite() error {
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

                func (s *Store) ListHistory(limit string) error {
                    _, err := s.db.Query("SELECT id, question FROM qa_entries LIMIT 10")
                    return err
                }

                func UnusedLegacyTopLevel() {}
                """);

            await File.WriteAllTextAsync(Path.Combine(storeDir, "users.go"), """
                package store

                import "errors"

                func (s *Store) RevokeSession(jti string) error {
                    if jti == "" {
                        return errors.New("invalid session")
                    }
                    _, err := s.db.Exec("INSERT INTO user_sessions (jti, revoked_at) VALUES (?, 'now')")
                    return err
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(httpApiDir, "handlers_auth.go"), """
                package httpapi

                import (
                    "net/http"
                    "policygraph/internal/auth"
                    "policygraph/internal/store"
                )

                type authHandlers struct {
                    auth  *auth.Manager
                    oidc  *auth.OIDCProvider
                    store *store.Store
                }

                func (h *authHandlers) handleLogout(w http.ResponseWriter, r *http.Request) {
                    _ = h.auth.RevokeToken("jti-1")
                    http.SetCookie(w, &http.Cookie{Name: "session", Value: "", MaxAge: -1})
                }
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
                    auth      *authHandlers
                    authMgr   *auth.Manager
                    store     *store.Store
                    retriever *cypher.Retriever
                    llm       *gemini.Client
                }

                func NewServer(authMgr *auth.Manager, oidc *auth.OIDCProvider, st *store.Store, ret *cypher.Retriever, llm *gemini.Client) *http.ServeMux {
                    s := &Server{
                        auth:      &authHandlers{auth: authMgr, oidc: oidc, store: st},
                        authMgr:   authMgr,
                        store:     st,
                        retriever: ret,
                        llm:       llm,
                    }
                    mux := http.NewServeMux()
                    mux.HandleFunc("POST /api/auth/logout", s.auth.handleLogout)
                    mux.HandleFunc("POST /api/ask/stream", s.withAuth(s.handleAsk))
                    mux.HandleFunc("GET /api/history", s.handleHistory)
                    mux.HandleFunc("GET /api/eggs", s.handleEggs)
                    return mux
                }

                func (s *Server) withAuth(next http.HandlerFunc) http.HandlerFunc {
                    return func(w http.ResponseWriter, r *http.Request) {
                        _, _ = s.authMgr.CurrentUser(r)
                        next(w, r)
                    }
                }

                func (s *Server) handleAsk(w http.ResponseWriter, r *http.Request) {
                    _ = s.retriever.QueryNodes(r.Context())
                    _, _ = s.llm.Summarize("q")
                    _ = s.store.SaveHistory("q")
                }

                func (s *Server) handleHistory(w http.ResponseWriter, r *http.Request) {
                    limit := r.URL.Query().Get("limit")
                    _ = s.store.ListHistory(limit)
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
                    "policygraph/internal/eastereggs"
                    "policygraph/internal/gemini"
                    "policygraph/internal/httpapi"
                    "policygraph/internal/store"
                )

                func main() {
                    cfg := config.Load()
                    pool, _ := pgxpool.New(context.Background(), cfg.PostgresDSN)
                    st, _ := store.New(cfg.SqlitePath)
                    _ = eastereggs.NewManager(st)
                    mgr := auth.NewManager("secret", st)
                    oidc := auth.NewOIDCProvider()
                    ret := cypher.NewRetriever(pool)
                    llm := gemini.NewClient("gemini-2.5-flash")
                    mux := httpapi.NewServer(mgr, oidc, st, ret, llm)
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

            // Bug #3: C3 view scopes POST:/api/auth/logout strictly to RevokeSession via SessionStore interface
            // without false errors.New -> store.New -> initSQLite DDL edges, duplicate Type:Store, URL.Query() SQL, or test queries
            var c3 = await viewEngine.GetComponentViewAsync("server");
            var logoutEp = c3.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Endpoint && n.Name == "POST:/api/auth/logout");
            var askEp = c3.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Endpoint && n.Name == "POST:/api/ask/stream");
            logoutEp.Should().NotBeNull();
            askEp.Should().NotBeNull();

            c3.Nodes.Where(n => n.Kind == NodeKind.Type && n.Name == "Store")
                .Should().ContainSingle("sibling receiver methods in users.go must not synthesize a duplicate Type:Store");

            c3.Nodes.Where(n => n.Kind == NodeKind.Query)
                .Should().NotContain(n => n.Name == "PostgreSQL: Query" || n.Name == "SQL: Query" || (n.FilePath != null && n.FilePath.EndsWith("_test.go")),
                    "zero-arg r.URL.Query()/u.Query() and *_test.go queries must not appear in C3");

            var oidcType = c3.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Type && n.Name == "OIDCProvider");
            var cypherClientType = c3.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Type && n.Name == "Client" && n.FilePath != null && n.FilePath.Contains("cypher"));
            var authHandlersType = c3.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Type && n.Name == "authHandlers");
            var authMgrType = c3.Nodes.FirstOrDefault(n => n.Kind == NodeKind.Type && n.Name == "Manager" && n.FilePath != null && n.FilePath.Contains("auth"));
            oidcType.Should().NotBeNull();
            cypherClientType.Should().NotBeNull();
            authHandlersType.Should().NotBeNull();
            authMgrType.Should().NotBeNull();

            c3.Edges.Should().NotContain(e => e.Source == oidcType!.Id && e.Target == cypherClientType!.Id,
                "*http.Client in OIDCProvider must not resolve to internal cypher.Client");
            c3.Edges.Should().Contain(e => e.Source == authHandlersType!.Id && e.Target == authMgrType!.Id,
                "package-qualified *auth.Manager on authHandlers must disambiguate auth.Manager from eastereggs.Manager");

            var logoutTargets = c3.Edges
                .Where(e => e.Source == logoutEp!.Id)
                .Select(e => c3.Nodes.FirstOrDefault(n => n.Id == e.Target))
                .OfType<GraphNodeDto>()
                .ToList();
            logoutTargets.Should().NotContain(n => n.Kind == NodeKind.Query && n.Name.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase),
                "errors.New inside RevokeToken/RevokeSession must not resolve to store.New -> initSQLite CREATE TABLE");
            logoutTargets.Should().Contain(n => n.Kind == NodeKind.Query && n.Name.Contains("user_sessions", StringComparison.OrdinalIgnoreCase),
                "POST /api/auth/logout -> handleLogout -> RevokeToken -> SessionStore.RevokeSession -> Store.RevokeSession must link to user_sessions DML");

            var askTargets = c3.Edges
                .Where(e => e.Source == askEp!.Id)
                .Select(e => c3.Nodes.FirstOrDefault(n => n.Id == e.Target))
                .OfType<GraphNodeDto>()
                .ToList();
            askTargets.Should().NotContain(n => n.Kind == NodeKind.Query && n.Name.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase),
                "POST /api/ask/stream must not inherit CREATE TABLE schema init queries from store.New");
            askTargets.Should().Contain(n =>
                (n.Kind == NodeKind.Query && (n.Name.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) || n.Name.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))) ||
                (n.Kind == NodeKind.Table && (n.Name == "qa_entries" || n.Name == "nodes")));

            // Bug #5: Incremental scan auto-invalidates when indexer_version changes
            var noOpChanged = (await indexer.IndexAsync(tempWorkspace)).HasChanges;
            noOpChanged.Should().BeFalse("matching indexer_version and unchanged files should skip rescan");

            await db.SetMetadataAsync(WorkspaceIndexer.IndexerVersionMetadataKey, "1.101.0");
            var versionUpgradeChanged = (await indexer.IndexAsync(tempWorkspace)).HasChanges;
            versionUpgradeChanged.Should().BeTrue("upgrading ce indexer_version must trigger a clean full rescan");
            var persistedVer = await db.GetMetadataAsync(WorkspaceIndexer.IndexerVersionMetadataKey);
            persistedVer.Should().Be(WorkspaceIndexer.CurrentIndexerVersion);

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
