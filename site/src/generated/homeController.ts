// Auto-generated Home Page interactive controller for React SPA
// @ts-nocheck
export function initHomeController(): () => void {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return () => {};
  }

    // Showcase Switcher Data
    const showcaseSlides = [
      {
        src: '/all_services.png?v=2',
        label: 'all_services.png',
        description: '<strong>System Map:</strong> Services, HTTP/gRPC routes, queues, and SQL tables built straight from source code.',
        pills: [
          '<span class="metric-chip metric-chip--accent">Synced with code</span>',
          '<span class="metric-chip metric-chip--ok">Local SQLite</span>',
          '<span class="metric-chip">Click to zoom</span>'
        ]
      },
      {
        src: '/selected_services.png?v=2',
        label: 'selected_services.png',
        description: '<strong>Focused Slice:</strong> Select the services you care about and hide surrounding noise while keeping indirect links.',
        pills: [
          '<span class="metric-chip metric-chip--accent">Blast radius</span>',
          '<span class="metric-chip metric-chip--ok">Noise filtered</span>',
          '<span class="metric-chip">Click to zoom</span>'
        ]
      },
      {
        src: '/project_flow.png',
        label: 'project_flow.png',
        description: '<strong>Service Call Flow:</strong> Trace HTTP calls, broker events, and database reads and writes before editing code.',
        pills: [
          '<span class="metric-chip metric-chip--accent">End-to-end flow</span>',
          '<span class="metric-chip metric-chip--ok">SQL and queue lineage</span>',
          '<span class="metric-chip">Click to zoom</span>'
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

      document.querySelectorAll('.workbench-tabs .tab-btn, #cockpit .tab-btn').forEach(b => b.classList.remove('active'));
      if (btnElement) btnElement.classList.add('active');

      img.style.opacity = '0.35';
      setTimeout(() => {
        img.src = slide.src;
        desc.innerHTML = slide.description;
        pills.innerHTML = slide.pills.join('');
        if (pathLabel) pathLabel.textContent = slide.label || slide.src;
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

    // Cypher Query Playground Presets
    const queryPresets = {
      blast_radius: {
        tag: 'blast_radius.cypher',
        raw: 'MATCH path = (caller:Function)-[:CALLS*1..4]->(target:Function {name: "ChargeAsync"}) RETURN caller.symbol AS UpstreamCaller, length(path) AS Hops ORDER BY Hops ASC LIMIT 10',
        codeHtml: `<span class="syn-kw">MATCH</span> path = (caller:Function)-[:CALLS*<span class="syn-num">1..4</span>]-&gt;(target:Function {name: <span class="syn-str">"ChargeAsync"</span>})
<span class="syn-kw">RETURN</span> caller.symbol <span class="syn-kw">AS</span> UpstreamCaller,
       <span class="syn-fn">length</span>(path) <span class="syn-kw">AS</span> Hops
<span class="syn-kw">ORDER BY</span> Hops <span class="syn-kw">ASC</span>
<span class="syn-kw">LIMIT</span> <span class="syn-num">10</span>`,
        explanation: 'Finds upstream callers within 4 hops that reach PaymentProcessor.ChargeAsync.',
        timing: '4 rows in 3.8 ms',
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
        raw: 'MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(:Function)-[:CALLS*1..3]->(h:Function)-[:HANDLES_COMMAND]->(cmd:Message), (h)-[:WRITES_TO]->(t:Table) RETURN ep.name AS Route, cmd.name AS Command, h.name AS Handler, t.name AS Table',
        codeHtml: `<span class="syn-kw">MATCH</span> (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]-&gt;(:Function)-[:CALLS*<span class="syn-num">1..3</span>]-&gt;(h:Function)-[:HANDLES_COMMAND]-&gt;(cmd:Message),
      (h)-[:WRITES_TO]-&gt;(t:Table)
<span class="syn-kw">RETURN</span> ep.name <span class="syn-kw">AS</span> Route,
       cmd.name <span class="syn-kw">AS</span> Command,
       h.name <span class="syn-kw">AS</span> Handler,
       t.name <span class="syn-kw">AS</span> Table`,
        explanation: 'Traces HTTP endpoints through CQRS command contracts to handler functions and mutated SQL tables.',
        timing: '3 rows in 4.2 ms',
        headers: ['Route', 'Command', 'Handler', 'Table'],
        rows: [
          ['POST /api/v1/orders', 'CreateOrderCommand', 'CreateOrderCommandHandler.Handle', 'purchase_orders'],
          ['POST /api/v1/refunds', 'IssueRefundCommand', 'IssueRefundCommandHandler.Handle', 'ledger_entries'],
          ['PUT /api/v1/inventory/reserve', 'ReserveStockCommand', 'ReserveStockHandler.Handle', 'stock_reservations']
        ]
      },
      api_to_db: {
        tag: 'api_to_db.cypher',
        raw: 'MATCH (s:Service)-[:IMPLEMENTED_BY]->(p:Project), (fn:Function)-[op:READS_FROM|WRITES_TO]->(tbl:Table {name: "customer_ledgers"}) WHERE fn.project_name = p.name RETURN s.name AS Service, fn.name AS Function, type(op) AS Operation',
        codeHtml: `<span class="syn-kw">MATCH</span> (s:Service)-[:IMPLEMENTED_BY]-&gt;(p:Project),
      (fn:Function)-[op:READS_FROM|WRITES_TO]-&gt;(tbl:Table {name: <span class="syn-str">"customer_ledgers"</span>})
<span class="syn-kw">WHERE</span> fn.project_name = p.name
<span class="syn-kw">RETURN</span> s.name <span class="syn-kw">AS</span> Service,
       fn.name <span class="syn-kw">AS</span> Function,
       <span class="syn-fn">type</span>(op) <span class="syn-kw">AS</span> Operation`,
        explanation: 'Finds every service and function reading from or writing to a database table.',
        timing: '4 rows in 2.9 ms',
        headers: ['Service', 'Function', 'Operation'],
        rows: [
          ['Billing.Service', 'LedgerRepository.AppendEntryAsync', 'WRITES_TO'],
          ['Billing.Service', 'BalanceQueryService.GetBalanceAsync', 'READS_FROM'],
          ['Reporting.Worker', 'DailyReconciliationJob.ExportLedgers', 'READS_FROM'],
          ['Fraud.Engine', 'RiskScorer.EvaluateRecentVelocity', 'READS_FROM']
        ]
      },
      cyclic_deps: {
        tag: 'cyclic_deps.cypher',
        raw: 'MATCH (a:Project)-[:DEPENDS_ON]->(b:Project)-[:DEPENDS_ON*1..3]->(a) WHERE a.name <> b.name RETURN DISTINCT a.name AS ProjectA, b.name AS ProjectB',
        codeHtml: `<span class="syn-kw">MATCH</span> (a:Project)-[:DEPENDS_ON]-&gt;(b:Project)-[:DEPENDS_ON*<span class="syn-num">1..3</span>]-&gt;(a)
<span class="syn-kw">WHERE</span> a.name &lt;&gt; b.name
<span class="syn-kw">RETURN DISTINCT</span> a.name <span class="syn-kw">AS</span> ProjectA,
                b.name <span class="syn-kw">AS</span> ProjectB`,
        explanation: 'Finds direct or multi-hop circular project dependencies across the workspace.',
        timing: '2 rows in 1.9 ms',
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
      copyText(cmd, 'Copied Cypher CLI command!');
    }

    // Semantic Invariant Gates (SIG) Presets
    const gatePresets = {
      boundaries: {
        file: '.codeexplorer/gates/domain-purity.yaml',
        dimension: 'Layer boundary',
        description: 'Blocks any type inside a Domain module from referencing Infrastructure or EF Core types.',
        status: 'Exit 1 (2 violations)',
        statusOk: false,
        rawYaml: `id: SIG-CA-B-001
name: Domain Layer Must Not Reference Infrastructure
severity: Error
query: |
  MATCH (d:Type)-[:DEPENDS_ON|CALLS|USES_TYPE]->(inf:Type)
  WHERE d.file_path CONTAINS "/Domain/"
    AND (inf.file_path CONTAINS "/Infrastructure/" OR inf.name CONTAINS "DbContext")
  RETURN d.file_path AS file, d.start_line AS line, d.name AS symbol, inf.name AS target`,
        yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">SIG-CA-B-001</span>
<span class="syn-kw">name:</span> <span class="syn-str">Domain Layer Must Not Reference Infrastructure</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (d:Type)-[:DEPENDS_ON|CALLS|USES_TYPE]-&gt;(inf:Type)
  <span class="syn-kw">WHERE</span> d.file_path <span class="syn-kw">CONTAINS</span> <span class="syn-str">"/Domain/"</span>
    <span class="syn-kw">AND</span> (inf.file_path <span class="syn-kw">CONTAINS</span> <span class="syn-str">"/Infrastructure/"</span>
      <span class="syn-kw">OR</span> inf.name <span class="syn-kw">CONTAINS</span> <span class="syn-str">"DbContext"</span>)
  <span class="syn-kw">RETURN</span> d.file_path <span class="syn-kw">AS</span> file, d.start_line <span class="syn-kw">AS</span> line, d.name <span class="syn-kw">AS</span> symbol, inf.name <span class="syn-kw">AS</span> target`,
        violations: [
          {
            rule: 'SIG-CA-B-001 (Error)',
            target: 'Orders.Domain.Aggregates.Order -> Orders.Infrastructure.Persistence.OrderDbContext',
            loc: 'src/Orders.Domain/Aggregates/Order.cs:84'
          },
          {
            rule: 'SIG-CA-B-001 (Error)',
            target: 'Billing.Domain.Services.InvoiceCalculator -> Billing.Infrastructure.Http.TaxRateClient',
            loc: 'src/Billing.Domain/Services/InvoiceCalculator.cs:41'
          }
        ]
      },
      topology: {
        file: '.codeexplorer/gates/no-shared-database-writes.yaml',
        dimension: 'Table ownership',
        description: 'Prevents two distinct services from writing directly to the same SQL table.',
        status: 'Exit 1 (1 violation)',
        statusOk: false,
        rawYaml: `id: SIG-DDD-001
name: No Shared Table Writes Across Services
severity: Error
query: |
  MATCH (s1:Service)-[:IMPLEMENTED_BY]->(p1:Project),
        (s2:Service)-[:IMPLEMENTED_BY]->(p2:Project),
        (w1:Function)-[:WRITES_TO]->(t:Table)<-[:WRITES_TO]-(w2:Function)
  WHERE s1.name <> s2.name AND w1.project_name = p1.name AND w2.project_name = p2.name
  RETURN DISTINCT w1, t, s1, w2, s2`,
        yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">SIG-DDD-001</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Shared Table Writes Across Services</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (s1:Service)-[:IMPLEMENTED_BY]-&gt;(p1:Project),
        (s2:Service)-[:IMPLEMENTED_BY]-&gt;(p2:Project),
        (w1:Function)-[:WRITES_TO]-&gt;(t:Table)&lt;-[:WRITES_TO]-(w2:Function)
  <span class="syn-kw">WHERE</span> s1.name &lt;&gt; s2.name
    <span class="syn-kw">AND</span> w1.project_name = p1.name <span class="syn-kw">AND</span> w2.project_name = p2.name
  <span class="syn-kw">RETURN DISTINCT</span> w1, t, s1, w2, s2`,
        violations: [
          {
            rule: 'SIG-DDD-001 (Error)',
            target: 'Table "stock_items" written by both Checkout.Service and Warehouse.Service',
            loc: 'src/Checkout.Service/Repositories/StockWriter.cs:59'
          }
        ]
      },
      visitor: {
        file: '.codeexplorer/gates/no-sync-over-async.yaml',
        dimension: 'Async safety',
        description: 'Catches blocking .Result or .Wait() calls inside async functions that starve thread pools.',
        status: 'Exit 1 (2 violations)',
        statusOk: false,
        rawYaml: `id: SIG-AST-012
name: No Blocking .Result or .Wait() Inside Async Functions
severity: Error
query: |
  MATCH (fn:Function {is_async: true})-[:CALLS]->(b:Function)
  WHERE b.name IN ["get_Result", "Wait", "GetResult"] AND NOT fn.is_test
  RETURN fn.file_path AS file, fn.start_line AS line, fn.name AS symbol, b.name AS target`,
        yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">SIG-AST-012</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Blocking .Result or .Wait() Inside Async Functions</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (fn:Function {is_async: <span class="syn-num">true</span>})-[:CALLS]-&gt;(b:Function)
  <span class="syn-kw">WHERE</span> b.name <span class="syn-kw">IN</span> [<span class="syn-str">"get_Result"</span>, <span class="syn-str">"Wait"</span>, <span class="syn-str">"GetResult"</span>] <span class="syn-kw">AND NOT</span> fn.is_test
  <span class="syn-kw">RETURN</span> fn.file_path <span class="syn-kw">AS</span> file, fn.start_line <span class="syn-kw">AS</span> line, fn.name <span class="syn-kw">AS</span> symbol, b.name <span class="syn-kw">AS</span> target`,
        violations: [
          {
            rule: 'SIG-AST-012 (Error)',
            target: 'Payments.Api.WebhookController.HandleStripeAsync calls Task.get_Result',
            loc: 'src/Payments.Api/Controllers/WebhookController.cs:112'
          },
          {
            rule: 'SIG-AST-012 (Error)',
            target: 'Notifications.Worker.EmailDispatcher.SendBatchAsync calls Task.Wait',
            loc: 'src/Notifications.Worker/EmailDispatcher.cs:67'
          }
        ]
      },
      literal: {
        file: '.codeexplorer/gates/no-hardcoded-urls.yaml',
        dimension: 'Config hygiene',
        description: 'Flags hardcoded internal service URLs or connection strings in non-test functions.',
        status: 'Exit 1 (1 violation)',
        statusOk: false,
        rawYaml: `id: SIG-SEC-003
name: No Hardcoded Internal Service URLs Outside Config
severity: Error
query: |
  MATCH (fn:Function)-[:PASSES_LITERAL]->(lit:CallLiteral)
  WHERE lit.value STARTS WITH "http://" AND NOT fn.is_test
  RETURN fn.file_path AS file, lit.line AS line, fn.name AS symbol, lit.value AS target`,
        yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">SIG-SEC-003</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Hardcoded Internal Service URLs Outside Config</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">query:</span> |
  <span class="syn-kw">MATCH</span> (fn:Function)-[:PASSES_LITERAL]-&gt;(lit:CallLiteral)
  <span class="syn-kw">WHERE</span> lit.value <span class="syn-kw">STARTS WITH</span> <span class="syn-str">"http://"</span>
    <span class="syn-kw">AND NOT</span> fn.is_test
  <span class="syn-kw">RETURN</span> fn.file_path <span class="syn-kw">AS</span> file, lit.line <span class="syn-kw">AS</span> line, fn.name <span class="syn-kw">AS</span> symbol, lit.value <span class="syn-kw">AS</span> target`,
        violations: [
          {
            rule: 'SIG-SEC-003 (Error)',
            target: 'Shipping.Infrastructure.CarrierClient passes literal "http://carrier-internal.prod:8080/v2"',
            loc: 'src/Shipping.Infrastructure/Clients/CarrierClient.cs:29'
          }
        ]
      },
      ratchet: {
        file: '.codeexplorer/baselines/SIG-CA-B-001.json',
        dimension: 'Baseline ratchet',
        description: 'Tolerates fingerprinted legacy violations while failing CI on any net-new violation.',
        status: 'Exit 0 (0 new, 2 tolerated)',
        statusOk: true,
        rawYaml: `{
  "gateId": "SIG-CA-B-001",
  "allowedViolations": 2,
  "entries": [
    {
      "fingerprint": "a5d81e9467416901e0131d28a725dff8e4ce2c55ca78f70a27ecc0269eec5afb",
      "file": "src/Orders.Domain/Aggregates/Order.cs",
      "line": 84,
      "symbol": "Order",
      "target": "OrderDbContext"
    },
    {
      "fingerprint": "15102b1a405cdd08a31e41205a2beabae835006a0a5eee9fd8be82de74409192",
      "file": "src/Billing.Domain/Services/InvoiceCalculator.cs",
      "line": 41,
      "symbol": "InvoiceCalculator",
      "target": "TaxRateClient"
    }
  ]
}`,
        yamlHtml: `{
  <span class="syn-kw">"gateId"</span>: <span class="syn-str">"SIG-CA-B-001"</span>,
  <span class="syn-kw">"allowedViolations"</span>: <span class="syn-num">2</span>,
  <span class="syn-kw">"entries"</span>: [
    {
      <span class="syn-kw">"fingerprint"</span>: <span class="syn-str">"a5d81e9467416901..."</span>,
      <span class="syn-kw">"file"</span>: <span class="syn-str">"src/Orders.Domain/Aggregates/Order.cs"</span>,
      <span class="syn-kw">"line"</span>: <span class="syn-num">84</span>,
      <span class="syn-kw">"symbol"</span>: <span class="syn-str">"Order"</span>,
      <span class="syn-kw">"target"</span>: <span class="syn-str">"OrderDbContext"</span>
    }
  ]
}`,
        violations: [
          {
            rule: 'RATCHET PASS (0 net-new violations)',
            target: '2 existing legacy violations matched baseline fingerprints; 0 new violations introduced in PR.',
            loc: '.codeexplorer/baselines/SIG-CA-B-001.json (verified in 6.4 ms)',
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
