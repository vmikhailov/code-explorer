// Auto-generated Home Page interactive controller for React SPA
// @ts-nocheck
export function initHomeController(): () => void {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return () => {};
  }

    // Showcase Switcher Data
    const showcaseSlides = [
      {
        src: 'all_services.png',
        description: '<strong>Full System Architecture Map:</strong> Every microservice, REST/gRPC endpoint, RabbitMQ/Kafka topic, and database table rendered automatically from your code. Double-click any service to drill into internal call chains.',
        pills: [
          '<span class="metric-chip metric-chip--accent">Always Synced With Code</span>',
          '<span class="metric-chip metric-chip--ok">Sub-10ms SQLite Queries</span>',
          '<span class="metric-chip">Click image to enlarge</span>'
        ]
      },
      {
        src: 'selected_services.png',
        description: '<strong>Focused Dependency Slice (Blast Radius):</strong> Pick only the services relevant to your task and hide the rest. CodeExplorer collapses intermediate hops into dashed indirect edges so you see the exact blast radius without visual clutter.',
        pills: [
          '<span class="metric-chip metric-chip--accent">Blast Radius View</span>',
          '<span class="metric-chip metric-chip--ok">Zero Noise Focus</span>',
          '<span class="metric-chip">Click image to enlarge</span>'
        ]
      },
      {
        src: 'project_flow.png',
        description: '<strong>Cross-Service Call Flows (API → Queue → DB):</strong> Trace synchronous HTTP/gRPC calls, asynchronous message broker publications, and SQL table reads/writes across project boundaries before modifying a single line of code.',
        pills: [
          '<span class="metric-chip metric-chip--accent">End-to-End Call Chains</span>',
          '<span class="metric-chip metric-chip--ok">SQL &amp; Broker Lineage</span>',
          '<span class="metric-chip">Click image to enlarge</span>'
        ]
      }
    ];

    function switchShowcase(index, btnElement) {
      const slide = showcaseSlides[index];
      if (!slide) return;
      const img = document.getElementById('showcaseImage');
      const desc = document.getElementById('showcaseDescription');
      const pills = document.getElementById('showcasePills');
      const pathLabel = document.getElementById('showcasePathLabel');

      document.querySelectorAll('#cockpit .tab-btn').forEach(b => b.classList.remove('active'));
      if (btnElement) btnElement.classList.add('active');

      img.style.opacity = '0.35';
      setTimeout(() => {
        img.src = slide.src;
        desc.innerHTML = slide.description;
        pills.innerHTML = slide.pills.join('');
        if (pathLabel) pathLabel.textContent = slide.src;
        img.style.opacity = '1';
      }, 120);
    }

    function openLightbox() {
      const modal = document.getElementById('lightboxModal');
      const modalImg = document.getElementById('lightboxImg');
      const currentImg = document.getElementById('showcaseImage');
      modalImg.src = currentImg.src;
      modal.classList.add('open');
    }

    function closeLightbox() {
      document.getElementById('lightboxModal').classList.remove('open');
    }

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') closeLightbox();
    });

    // OpenCypher Query Playground Presets
    const queryPresets = {
      blast_radius: {
        tag: 'blast_radius.cypher',
        raw: 'MATCH path = (caller:Method)-[:CALLS*1..4]->(target:Method {name: "ChargeAsync"}) RETURN caller.fullName AS UpstreamCaller, length(path) AS Hops ORDER BY Hops ASC LIMIT 10',
        codeHtml: `<span class="syn-kw">MATCH</span> path = (caller:Method)-[:CALLS*<span class="syn-num">1..4</span>]-&gt;(target:Method {name: <span class="syn-str">"ChargeAsync"</span>})
<span class="syn-kw">RETURN</span> caller.fullName <span class="syn-kw">AS</span> UpstreamCaller,
       <span class="syn-fn">length</span>(path) <span class="syn-kw">AS</span> Hops
<span class="syn-kw">ORDER BY</span> Hops <span class="syn-kw">ASC</span>
<span class="syn-kw">LIMIT</span> <span class="syn-num">10</span>`,
        explanation: 'Finds all upstream callers within 4 hops that can impact PaymentProcessor.ChargeAsync.',
        timing: '4 rows · 3.8 ms',
        headers: ['UpstreamCaller', 'Hops'],
        rows: [
          ['Billing.Application.Commands.CaptureOrderHandler.Handle', '1'],
          ['Orders.Api.Controllers.CheckoutController.SubmitOrder', '2'],
          ['Subscriptions.Worker.Consumers.RenewalConsumer.Consume', '2'],
          ['Gateway.Bff.Endpoints.MobileCheckoutEndpoint.Execute', '3']
        ]
      },
      cqrs_pipeline: {
        tag: 'cqrs_pipeline.cypher',
        raw: 'MATCH (ep:Endpoint)-[:CALLS*1..2]->(cmd:Class)-[:HANDLED_BY]->(h:Class)-[:WRITES_TO]->(t:SqlTable) RETURN ep.route AS Route, cmd.name AS Command, h.name AS Handler, t.name AS Table',
        codeHtml: `<span class="syn-kw">MATCH</span> (ep:Endpoint)-[:CALLS*<span class="syn-num">1..2</span>]-&gt;(cmd:Class)-[:HANDLED_BY]-&gt;(h:Class)-[:WRITES_TO]-&gt;(t:SqlTable)
<span class="syn-kw">RETURN</span> ep.route <span class="syn-kw">AS</span> Route,
       cmd.name <span class="syn-kw">AS</span> Command,
       h.name <span class="syn-kw">AS</span> Handler,
       t.name <span class="syn-kw">AS</span> Table`,
        explanation: 'Traces HTTP endpoints through MediatR/CQRS commands to handlers and mutated SQL tables.',
        timing: '3 rows · 4.2 ms',
        headers: ['Route', 'Command', 'Handler', 'Table'],
        rows: [
          ['POST /api/v1/orders', 'CreateOrderCommand', 'CreateOrderCommandHandler', 'orders.purchase_orders'],
          ['POST /api/v1/refunds', 'IssueRefundCommand', 'IssueRefundCommandHandler', 'billing.ledger_entries'],
          ['PUT /api/v1/inventory/reserve', 'ReserveStockCommand', 'ReserveStockHandler', 'warehouse.stock_reservations']
        ]
      },
      api_to_db: {
        tag: 'api_to_db.cypher',
        raw: 'MATCH (s:Service)-[:CONTAINS*]->(m:Method)-[op:READS_FROM|WRITES_TO]->(tbl:SqlTable {name: "customer_ledgers"}) RETURN s.name AS Service, m.name AS Method, type(op) AS Operation',
        codeHtml: `<span class="syn-kw">MATCH</span> (s:Service)-[:CONTAINS*]-&gt;(m:Method)-[op:READS_FROM|WRITES_TO]-&gt;(tbl:SqlTable {name: <span class="syn-str">"customer_ledgers"</span>})
<span class="syn-kw">RETURN</span> s.name <span class="syn-kw">AS</span> Service,
       m.name <span class="syn-kw">AS</span> Method,
       <span class="syn-fn">type</span>(op) <span class="syn-kw">AS</span> Operation`,
        explanation: 'Discovers every microservice and repository method reading from or writing to a shared database table.',
        timing: '4 rows · 2.9 ms',
        headers: ['Service', 'Method', 'Operation'],
        rows: [
          ['Billing.Service', 'LedgerRepository.AppendEntryAsync', 'WRITES_TO'],
          ['Billing.Service', 'BalanceQueryService.GetBalanceAsync', 'READS_FROM'],
          ['Reporting.Worker', 'DailyReconciliationJob.ExportLedgers', 'READS_FROM'],
          ['Fraud.Engine', 'RiskScorer.EvaluateRecentVelocity', 'READS_FROM']
        ]
      },
      cyclic_deps: {
        tag: 'cyclic_deps.cypher',
        raw: 'MATCH (a:Project)-[:DEPENDS_ON]->(b:Project)-[:DEPENDS_ON*1..3]->(a) WHERE a.name < b.name RETURN a.name AS ProjectA, b.name AS ProjectB',
        codeHtml: `<span class="syn-kw">MATCH</span> (a:Project)-[:DEPENDS_ON]-&gt;(b:Project)-[:DEPENDS_ON*<span class="syn-num">1..3</span>]-&gt;(a)
<span class="syn-kw">WHERE</span> a.name &lt; b.name
<span class="syn-kw">RETURN</span> a.name <span class="syn-kw">AS</span> ProjectA,
       b.name <span class="syn-kw">AS</span> ProjectB`,
        explanation: 'Detects direct or multi-hop circular project dependencies across the solution.',
        timing: '2 rows · 1.9 ms',
        headers: ['ProjectA', 'ProjectB'],
        rows: [
          ['Orders.Application', 'Shared.Fulfillment.Contracts'],
          ['Identity.Core', 'Tenancy.Resolution']
        ]
      }
    };

    let activeQueryKey = 'blast_radius';

    function loadQueryPreset(key, btnElement) {
      const preset = queryPresets[key];
      if (!preset) return;
      activeQueryKey = key;

      document.querySelectorAll('#playground .preset-btn').forEach(b => b.classList.remove('active'));
      if (btnElement) btnElement.classList.add('active');

      document.getElementById('queryPresetTag').textContent = preset.tag;
      document.getElementById('queryCodeArea').innerHTML = preset.codeHtml;
      document.getElementById('queryExplanation').textContent = preset.explanation;
      document.getElementById('queryTimingBadge').textContent = preset.timing;

      const ths = preset.headers.map(h => `<th>${h}</th>`).join('');
      const trs = preset.rows.map(row => `<tr>${row.map(c => `<td>${c}</td>`).join('')}</tr>`).join('');
      document.getElementById('queryResultsArea').innerHTML = `<table class="data-table"><thead><tr>${ths}</tr></thead><tbody>${trs}</tbody></table>`;
    }

    function runQueryAnimation() {
      const btn = document.getElementById('runQueryBtn');
      const badge = document.getElementById('queryTimingBadge');
      const preset = queryPresets[activeQueryKey];
      btn.textContent = 'Running...';
      badge.textContent = 'Executing query...';
      setTimeout(() => {
        btn.textContent = 'Run Query';
        badge.textContent = preset.timing;
      }, 180);
    }

    function copyCurrentQuery() {
      const preset = queryPresets[activeQueryKey];
      const cmd = `ce query '${preset.raw}'`;
      copyText(cmd, 'Copied OpenCypher CLI command!');
    }

    // Semantic Invariant Gates (SIG) Presets
    const gatePresets = {
      boundaries: {
        file: '.codeexplorer/gates/domain-purity.yaml',
        dimension: 'Dimension: Boundaries (L2/L3)',
        description: 'Blocks any type inside a Domain module from depending on Infrastructure or EF Core types.',
        status: 'Exit Code 1 · 2 Violations',
        statusOk: false,
        rawYaml: `id: ARCH-001
name: Domain Layer Must Not Reference Infrastructure
severity: error
query: |
  MATCH (d:Class)-[:DEPENDS_ON|CALLS]->(inf:Class)
  WHERE d.namespace CONTAINS ".Domain"
    AND (inf.namespace CONTAINS ".Infrastructure" OR inf.namespace CONTAINS "Microsoft.EntityFrameworkCore")
  RETURN d.fullName AS Violator, inf.fullName AS ForbiddenTarget`,
        yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">ARCH-001</span>
<span class="syn-kw">name:</span> <span class="syn-str">Domain Layer Must Not Reference Infrastructure</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (d:Class)-[:DEPENDS_ON|CALLS]-&gt;(inf:Class)
  <span class="syn-kw">WHERE</span> d.namespace <span class="syn-kw">CONTAINS</span> <span class="syn-str">".Domain"</span>
    <span class="syn-kw">AND</span> (inf.namespace <span class="syn-kw">CONTAINS</span> <span class="syn-str">".Infrastructure"</span>
      <span class="syn-kw">OR</span> inf.namespace <span class="syn-kw">CONTAINS</span> <span class="syn-str">"Microsoft.EntityFrameworkCore"</span>)
  <span class="syn-kw">RETURN</span> d.fullName <span class="syn-kw">AS</span> Violator, inf.fullName <span class="syn-kw">AS</span> ForbiddenTarget`,
        violations: [
          {
            rule: 'ARCH-001 (error)',
            target: 'Orders.Domain.Aggregates.Order -> Orders.Infrastructure.Persistence.OrderDbContext',
            loc: 'src/Orders.Domain/Aggregates/Order.cs:84'
          },
          {
            rule: 'ARCH-001 (error)',
            target: 'Billing.Domain.Services.InvoiceCalculator -> Billing.Infrastructure.Http.TaxRateClient',
            loc: 'src/Billing.Domain/Services/InvoiceCalculator.cs:41'
          }
        ]
      },
      topology: {
        file: '.codeexplorer/gates/no-shared-database-writes.yaml',
        dimension: 'Dimension: Topology (L4/L5)',
        description: 'Forbids two distinct microservices from writing to the same SQL table directly.',
        status: 'Exit Code 1 · 1 Violation',
        statusOk: false,
        rawYaml: `id: TOPO-004
name: Single-Writer Microservice Table Ownership
severity: error
query: |
  MATCH (s1:Service)-[:CONTAINS*]->(:Method)-[:WRITES_TO]->(t:SqlTable)<-[:WRITES_TO]-(:Method)<-[:CONTAINS*]-(s2:Service)
  WHERE s1.name < s2.name
  RETURN t.name AS SharedTable, s1.name AS WriterA, s2.name AS WriterB`,
        yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">TOPO-004</span>
<span class="syn-kw">name:</span> <span class="syn-str">Single-Writer Microservice Table Ownership</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (s1:Service)-[:CONTAINS*]-&gt;(:Method)-[:WRITES_TO]-&gt;(t:SqlTable)
        &lt;-[:WRITES_TO]-(:Method)&lt;-[:CONTAINS*]-(s2:Service)
  <span class="syn-kw">WHERE</span> s1.name &lt; s2.name
  <span class="syn-kw">RETURN</span> t.name <span class="syn-kw">AS</span> SharedTable, s1.name <span class="syn-kw">AS</span> WriterA, s2.name <span class="syn-kw">AS</span> WriterB`,
        violations: [
          {
            rule: 'TOPO-004 (error)',
            target: 'Table "inventory.stock_items" written by both Checkout.Service and Warehouse.Service',
            loc: 'src/Checkout.Service/Repositories/StockWriter.cs:59'
          }
        ]
      },
      visitor: {
        file: '.codeexplorer/gates/no-sync-over-async.yaml',
        dimension: 'Dimension: AST Visitor (L1/L3)',
        description: 'Catches blocking .Result or .Wait() calls inside async methods that cause thread-pool starvation.',
        status: 'Exit Code 1 · 2 Violations',
        statusOk: false,
        rawYaml: `id: AST-012
name: No Blocking .Result or .Wait() Inside Async Methods
severity: error
query: |
  MATCH (m:Method {isAsync: true})-[:CALLS]->(b:Method)
  WHERE b.name IN ["get_Result", "Wait", "GetResult"]
  RETURN m.fullName AS AsyncMethod, b.name AS BlockingCall, m.filePath AS File`,
        yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">AST-012</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Blocking .Result or .Wait() Inside Async Methods</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (m:Method {isAsync: <span class="syn-num">true</span>})-[:CALLS]-&gt;(b:Method)
  <span class="syn-kw">WHERE</span> b.name <span class="syn-kw">IN</span> [<span class="syn-str">"get_Result"</span>, <span class="syn-str">"Wait"</span>, <span class="syn-str">"GetResult"</span>]
  <span class="syn-kw">RETURN</span> m.fullName <span class="syn-kw">AS</span> AsyncMethod, b.name <span class="syn-kw">AS</span> BlockingCall`,
        violations: [
          {
            rule: 'AST-012 (error)',
            target: 'Payments.Api.WebhookController.HandleStripeAsync calls Task.get_Result',
            loc: 'src/Payments.Api/Controllers/WebhookController.cs:112'
          },
          {
            rule: 'AST-012 (error)',
            target: 'Notifications.Worker.EmailDispatcher.SendBatchAsync calls Task.Wait',
            loc: 'src/Notifications.Worker/EmailDispatcher.cs:67'
          }
        ]
      },
      literal: {
        file: '.codeexplorer/gates/no-hardcoded-urls.yaml',
        dimension: 'Dimension: Literal Audit (L1)',
        description: 'Prevents AI agents from hardcoding internal service URLs or connection strings in production classes.',
        status: 'Exit Code 1 · 1 Violation',
        statusOk: false,
        rawYaml: `id: SEC-003
name: No Hardcoded Internal Service URLs Outside Config
severity: error
query: |
  MATCH (c:Class)-[:CONTAINS*]->(lit:Literal)
  WHERE lit.value STARTS WITH "http://" AND NOT c.filePath CONTAINS "Tests"
  RETURN c.fullName AS ClassName, lit.value AS HardcodedLiteral`,
        yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">SEC-003</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Hardcoded Internal Service URLs Outside Config</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (c:Class)-[:CONTAINS*]-&gt;(lit:Literal)
  <span class="syn-kw">WHERE</span> lit.value <span class="syn-kw">STARTS WITH</span> <span class="syn-str">"http://"</span>
    <span class="syn-kw">AND NOT</span> c.filePath <span class="syn-kw">CONTAINS</span> <span class="syn-str">"Tests"</span>
  <span class="syn-kw">RETURN</span> c.fullName <span class="syn-kw">AS</span> ClassName, lit.value <span class="syn-kw">AS</span> HardcodedLiteral`,
        violations: [
          {
            rule: 'SEC-003 (error)',
            target: 'Shipping.Infrastructure.CarrierClient contains literal "http://carrier-internal.prod:8080/v2"',
            loc: 'src/Shipping.Infrastructure/Clients/CarrierClient.cs:29'
          }
        ]
      },
      ratchet: {
        file: '.codeexplorer/gate-baseline.json',
        dimension: 'Dimension: Ratchet Baseline',
        description: 'Suppresses fingerprinted legacy debt while failing CI immediately on any net-new violation.',
        status: 'Exit Code 0 · 0 Net-New (3 Baselined)',
        statusOk: true,
        rawYaml: `{
  "schemaVersion": 1,
  "ratchetMode": "strict",
  "baselinedViolations": [
    { "gateId": "ARCH-001", "fingerprint": "sha256:9f84c2e1a0b3", "owner": "legacy-orders" },
    { "gateId": "TOPO-004", "fingerprint": "sha256:4d19b7c8e512", "owner": "legacy-warehouse" },
    { "gateId": "AST-012",  "fingerprint": "sha256:7a31e0f4c920", "owner": "legacy-billing" }
  ]
}`,
        yamlHtml: `{
  <span class="syn-kw">"schemaVersion"</span>: <span class="syn-num">1</span>,
  <span class="syn-kw">"ratchetMode"</span>: <span class="syn-str">"strict"</span>,
  <span class="syn-kw">"baselinedViolations"</span>: [
    { <span class="syn-kw">"gateId"</span>: <span class="syn-str">"ARCH-001"</span>, <span class="syn-kw">"fingerprint"</span>: <span class="syn-str">"sha256:9f84c2e1a0b3"</span> },
    { <span class="syn-kw">"gateId"</span>: <span class="syn-str">"TOPO-004"</span>, <span class="syn-kw">"fingerprint"</span>: <span class="syn-str">"sha256:4d19b7c8e512"</span> },
    { <span class="syn-kw">"gateId"</span>: <span class="syn-str">"AST-012"</span>,  <span class="syn-kw">"fingerprint"</span>: <span class="syn-str">"sha256:7a31e0f4c920"</span> }
  ]
}`,
        violations: [
          {
            rule: 'RATCHET PASS (0 net-new violations)',
            target: '3 known legacy violations matched baseline fingerprints; 0 new violations introduced in PR.',
            loc: '.codeexplorer/gate-baseline.json · verified in 6.4 ms',
            ok: true
          }
        ]
      }
    };

    let activeGateKey = 'boundaries';

    function loadGatePreset(key, btnElement) {
      const preset = gatePresets[key];
      if (!preset) return;
      activeGateKey = key;

      document.querySelectorAll('#gates .preset-btn').forEach(b => b.classList.remove('active'));
      if (btnElement) btnElement.classList.add('active');

      document.getElementById('gateFileLabel').textContent = preset.file;
      document.getElementById('gateDimensionBadge').textContent = preset.dimension;
      document.getElementById('gateYamlArea').innerHTML = preset.yamlHtml;
      document.getElementById('gateDescriptionText').textContent = preset.description;

      const statusBadge = document.getElementById('gateStatusBadge');
      statusBadge.textContent = preset.status;
      statusBadge.className = preset.statusOk ? 'measured tone-ok' : 'measured tone-bad';

      const listHtml = preset.violations.map(v => `
        <div class="violation-item ${v.ok ? 'violation-item--ok' : ''}">
          <strong class="${v.ok ? 'tone-ok' : 'tone-bad'}">${v.rule}</strong>
          <span>${v.target}</span>
          <span class="tone-muted">${v.loc}</span>
        </div>
      `).join('');
      document.getElementById('gateViolationsArea').innerHTML = listHtml;
    }

    function copyCurrentGateYaml() {
      const preset = gatePresets[activeGateKey];
      copyText(preset.rawYaml, 'Copied Gate configuration to clipboard!');
    }

    // Installation Tabs Switcher
    function switchInstallTab(tabId, btnElement) {
      document.querySelectorAll('#install .tab-btn').forEach(b => b.classList.remove('active'));
      if (btnElement) btnElement.classList.add('active');
      document.querySelectorAll('#install .install-pane').forEach(p => p.classList.remove('active'));
      const target = document.getElementById('tab-' + tabId);
      if (target) target.classList.add('active');
    }

    function copyInstallCmd(cmd) {
      copyText(cmd, 'Copied install command to clipboard!');
    }

    function copyMCPConfig() {
      const config = `{\n  "mcpServers": {\n    "code-explorer": {\n      "command": "ce",\n      "args": ["mcp"]\n    }\n  }\n}`;
      copyText(config, 'Copied MCP Server JSON config!');
    }

    function copyText(text, message) {
      try {
        const evt = text.includes('brew ') ? '/event/copy-brew' : (text.includes('mcp') ? '/event/copy-mcp' : '/event/copy-cli');
        if (navigator.sendBeacon) navigator.sendBeacon(evt);
      } catch (_) {}
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(() => showToast(message));
      } else {
        const ta = document.createElement('textarea');
        ta.value = text;
        document.body.appendChild(ta);
        ta.select();
        document.execCommand('copy');
        document.body.removeChild(ta);
        showToast(message);
      }
    }

    function showToast(message) {
      const toast = document.getElementById('toast');
      toast.textContent = message;
      toast.classList.add('show');
      setTimeout(() => toast.classList.remove('show'), 2400);
    }

    // Initialize interactive playgrounds on load
    
  window.switchShowcase = switchShowcase;
  window.openLightbox = openLightbox;
  window.closeLightbox = closeLightbox;
  window.loadQueryPreset = loadQueryPreset;
  window.runQueryAnimation = runQueryAnimation;
  window.copyCurrentQuery = copyCurrentQuery;
  window.loadGatePreset = loadGatePreset;
  window.copyCurrentGateYaml = copyCurrentGateYaml;
  window.switchInstallTab = switchInstallTab;
  window.copyInstallCmd = copyInstallCmd;
  window.copyMCPConfig = copyMCPConfig;

  const firstQueryBtn = document.querySelector('#playground .preset-btn');
  if (firstQueryBtn) loadQueryPreset('blast_radius', firstQueryBtn);
  const firstGateBtn = document.querySelector('#gates .preset-btn');
  if (firstGateBtn) loadGatePreset('boundaries', firstGateBtn);

  fetch('/version.json', { cache: 'no-cache' })
    .then(r => (r.ok ? r.json() : null))
    .then(data => {
      const rawVer = data && (data.tag_name || data.version);
      if (rawVer) {
        const v = rawVer.startsWith('v') ? rawVer : 'v' + rawVer;
        const heroBadge = document.getElementById('heroReleaseBadge');
        const footerVer = document.getElementById('footerCeVersion');
        if (heroBadge) heroBadge.textContent = v;
        if (footerVer) footerVer.textContent = 'CodeExplorer CLI & Extension ' + v;
      }
    })
    .catch(() => {});

  return () => {};
}
