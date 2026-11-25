// Home Page interactive controller for React SPA ("star chart" ver1 layout)
// @ts-nocheck
export function initHomeController(): () => void {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return () => {};
  }

  document.documentElement.classList.add('js');

  let toastTimer: ReturnType<typeof setTimeout> | undefined;
  function showToast(message: string): void {
    const toast = document.getElementById('toast');
    if (!toast) return;
    toast.textContent = message;
    toast.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toast.classList.remove('show'), 2000);
  }

  function copyText(text: string, message = 'Copied to clipboard'): void {
    try {
      const evt = text.includes('brew ')
        ? '/event/copy-brew'
        : text.includes('mcp')
          ? '/event/copy-mcp'
          : '/event/copy-cli';
      if (navigator.sendBeacon) navigator.sendBeacon(evt);
    } catch {
      // Ignore analytics beacon errors
    }

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard
        .writeText(text)
        .then(() => showToast(message))
        .catch(() => showToast('Select the command text to copy'));
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

  // Lightbox preview for architecture screenshots
  function openLightboxSrc(src: string): void {
    const modal = document.getElementById('lightboxModal');
    const modalImg = document.getElementById('lightboxImg') as HTMLImageElement | null;
    if (!modal || !modalImg) return;
    modalImg.src = src;
    modal.classList.add('open');
  }

  function closeLightbox(): void {
    const modal = document.getElementById('lightboxModal');
    if (modal) modal.classList.remove('open');
  }

  const lightboxModal = document.getElementById('lightboxModal');
  const onKeyDown = (e: KeyboardEvent): void => {
    if (e.key === 'Escape') closeLightbox();
  };
  if (lightboxModal) {
    lightboxModal.onclick = () => closeLightbox();
  }
  document.addEventListener('keydown', onKeyDown);

  document.querySelectorAll<HTMLElement>('[data-lightbox-src]').forEach((el) => {
    el.onclick = () => {
      const src = el.getAttribute('data-lightbox-src');
      if (src) openLightboxSrc(src);
    };
  });

  // Hero & inline [data-copy] buttons
  document.querySelectorAll<HTMLElement>('[data-copy]').forEach((el) => {
    el.onclick = () => {
      const cmd = el.getAttribute('data-copy');
      if (cmd) copyText(cmd, 'Install command copied');
    };
  });

  // Accessible tablists (roving tabindex + arrow keys)
  function wireTablist(list: Element, onSelect: (tab: HTMLElement) => void): void {
    const tabs = Array.from(list.querySelectorAll<HTMLElement>('[role="tab"]'));
    const select = (tab: HTMLElement, focus: boolean): void => {
      tabs.forEach((t) => {
        const active = t === tab;
        t.setAttribute('aria-selected', String(active));
        t.classList.toggle('active', active);
        t.tabIndex = active ? 0 : -1;
      });
      if (focus) tab.focus();
      onSelect(tab);
    };

    tabs.forEach((tab, idx) => {
      tab.onclick = () => select(tab, false);
      tab.onkeydown = (e: KeyboardEvent) => {
        const vertical = list.getAttribute('aria-orientation') === 'vertical';
        const nextKey = vertical ? 'ArrowDown' : 'ArrowRight';
        const prevKey = vertical ? 'ArrowUp' : 'ArrowLeft';
        let nextIdx: number | null = null;
        if (e.key === nextKey) nextIdx = (idx + 1) % tabs.length;
        if (e.key === prevKey) nextIdx = (idx - 1 + tabs.length) % tabs.length;
        if (e.key === 'Home') nextIdx = 0;
        if (e.key === 'End') nextIdx = tabs.length - 1;
        if (nextIdx !== null) {
          e.preventDefault();
          select(tabs[nextIdx], true);
        }
      };
    });
  }

  // 1. Readers vertical tabs
  const readerList = document.querySelector('.page-home .reader-list');
  if (readerList) {
    wireTablist(readerList, (tab) => {
      readerList.querySelectorAll<HTMLElement>('[role="tab"]').forEach((t) => {
        const panelId = t.getAttribute('aria-controls');
        const panel = panelId ? document.getElementById(panelId) : null;
        if (panel) panel.hidden = t !== tab;
      });
    });
  }

  const copyMcpBtn = document.querySelector<HTMLElement>('[data-copy-mcp]');
  if (copyMcpBtn) {
    copyMcpBtn.onclick = () => {
      const config = `{\n  "mcpServers": {\n    "code-explorer": {\n      "command": "ce",\n      "args": ["mcp"]\n    }\n  }\n}`;
      copyText(config, 'MCP server JSON copied');
    };
  }

  // 2. Cypher Query Playground Presets (verified CodeExplorer ontology)
  const queryPresets = {
    blast_radius: {
      tag: 'blast_radius.cypher',
      raw: 'MATCH path = (caller:Function)-[:CALLS*1..4]->(target:Function {name: "ChargeAsync"}) RETURN caller.symbol AS UpstreamCaller, length(path) AS Hops ORDER BY Hops ASC LIMIT 10',
      codeHtml: `<span class="syn-kw">MATCH</span> path = (caller:Function)-[:CALLS*<span class="syn-num">1..4</span>]-&gt;
      (target:Function {name: <span class="syn-str">"ChargeAsync"</span>})
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
        ['Gateway.Bff.Endpoints.MobileCheckoutEndpoint.Execute', '3'],
      ],
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
        ['PUT /api/v1/inventory/reserve', 'ReserveStockCommand', 'ReserveStockHandler.Handle', 'stock_reservations'],
      ],
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
        ['Fraud.Engine', 'RiskScorer.EvaluateRecentVelocity', 'READS_FROM'],
      ],
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
        ['Identity.Core', 'Tenancy.Resolution'],
      ],
    },
  };

  const esc = (s: string): string =>
    s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c] || c));

  let activeQueryKey = 'blast_radius';
  const queryTabs = document.querySelector('#playground [role="tablist"]');
  if (queryTabs) {
    wireTablist(queryTabs, (tab) => {
      const key = tab.getAttribute('data-preset') || 'blast_radius';
      const preset = queryPresets[key];
      if (!preset) return;
      activeQueryKey = key;

      const tagEl = document.getElementById('queryPresetTag');
      const codeEl = document.getElementById('queryCodeArea');
      const expEl = document.getElementById('queryExplanation');
      const timeEl = document.getElementById('queryTimingBadge');
      const resEl = document.getElementById('queryResultsArea');

      if (tagEl) tagEl.textContent = preset.tag;
      if (codeEl) codeEl.innerHTML = preset.codeHtml;
      if (expEl) expEl.textContent = preset.explanation;
      if (timeEl) timeEl.textContent = preset.timing;
      if (resEl) {
        const ths = preset.headers.map((h) => `<th>${esc(h)}</th>`).join('');
        const trs = preset.rows
          .map((row) => `<tr>${row.map((c) => `<td>${esc(c)}</td>`).join('')}</tr>`)
          .join('');
        resEl.innerHTML = `<table class="table"><thead><tr>${ths}</tr></thead><tbody>${trs}</tbody></table>`;
      }
    });

    const copyQueryBtn = document.querySelector<HTMLElement>('[data-copy-query]');
    if (copyQueryBtn) {
      copyQueryBtn.onclick = () => {
        const preset = queryPresets[activeQueryKey];
        if (preset) copyText(`ce query '${preset.raw}'`, 'Cypher CLI command copied');
      };
    }
  }

  // 3. Semantic Invariant Gates (SIG) Presets
  const gatePresets = {
    boundaries: {
      file: '.codeexplorer/gates/domain-purity.yaml',
      description: 'Blocks any type inside a Domain module from referencing Infrastructure or EF Core types.',
      status: 'Exit 1 · 2 violations',
      statusOk: false,
      rawYaml: `id: SIG-CA-B-001
name: Domain Layer Must Not Reference Infrastructure
severity: Error
target: "{inf.name}"
message: "Domain type '{d.name}' references infrastructure type '{inf.name}'"
query: |-
  MATCH (d:Type)-[:DEPENDS_ON|INHERITS|IMPLEMENTS]->(inf:Type)
  WHERE d.file_path CONTAINS "/Domain/"
    AND (inf.file_path CONTAINS "/Infrastructure/" OR inf.symbol CONTAINS "DbContext")
  RETURN d, inf`,
      yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">SIG-CA-B-001</span>
<span class="syn-kw">name:</span> <span class="syn-str">Domain Layer Must Not Reference Infrastructure</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">target:</span> <span class="syn-str">"{inf.name}"</span>
<span class="syn-kw">message:</span> <span class="syn-str">"Domain type '{d.name}' references infrastructure type '{inf.name}'"</span>
<span class="syn-kw">query:</span> |-
  <span class="syn-kw">MATCH</span> (d:Type)-[:DEPENDS_ON|INHERITS|IMPLEMENTS]-&gt;(inf:Type)
  <span class="syn-kw">WHERE</span> d.file_path <span class="syn-kw">CONTAINS</span> <span class="syn-str">"/Domain/"</span>
    <span class="syn-kw">AND</span> (inf.file_path <span class="syn-kw">CONTAINS</span> <span class="syn-str">"/Infrastructure/"</span>
      <span class="syn-kw">OR</span> inf.symbol <span class="syn-kw">CONTAINS</span> <span class="syn-str">"DbContext"</span>)
  <span class="syn-kw">RETURN</span> d, inf`,
      violations: [
        {
          rule: 'SIG-CA-B-001 · Error',
          target: 'Orders.Domain.Aggregates.Order -> Orders.Infrastructure.Persistence.OrderDbContext',
          loc: 'src/Orders.Domain/Aggregates/Order.cs:84',
        },
        {
          rule: 'SIG-CA-B-001 · Error',
          target: 'Billing.Domain.Services.InvoiceCalculator -> Billing.Infrastructure.Http.TaxRateClient',
          loc: 'src/Billing.Domain/Services/InvoiceCalculator.cs:41',
        },
      ],
    },
    topology: {
      file: '.codeexplorer/gates/no-shared-database-writes.yaml',
      description: 'Prevents two distinct services from writing directly to the same SQL table.',
      status: 'Exit 1 · 1 violation',
      statusOk: false,
      rawYaml: `id: SIG-DDD-001
name: No Shared Table Writes Across Services
severity: Error
target: "{t.name}"
message: "Table '{t.name}' written by both '{s1.name}' and '{s2.name}'"
query: |-
  MATCH (s1:Service)-[:IMPLEMENTED_BY]->(p1:Project),
        (s2:Service)-[:IMPLEMENTED_BY]->(p2:Project),
        (w1:Function)-[:WRITES_TO]->(t:Table)<-[:WRITES_TO]-(w2:Function)
  WHERE s1.name <> s2.name AND w1.project_name = p1.name AND w2.project_name = p2.name
  RETURN DISTINCT w1, t, s1, w2, s2`,
      yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">SIG-DDD-001</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Shared Table Writes Across Services</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">target:</span> <span class="syn-str">"{t.name}"</span>
<span class="syn-kw">message:</span> <span class="syn-str">"Table '{t.name}' written by both '{s1.name}' and '{s2.name}'"</span>
<span class="syn-kw">query:</span> |-
  <span class="syn-kw">MATCH</span> (s1:Service)-[:IMPLEMENTED_BY]-&gt;(p1:Project),
        (s2:Service)-[:IMPLEMENTED_BY]-&gt;(p2:Project),
        (w1:Function)-[:WRITES_TO]-&gt;(t:Table)&lt;-[:WRITES_TO]-(w2:Function)
  <span class="syn-kw">WHERE</span> s1.name &lt;&gt; s2.name
    <span class="syn-kw">AND</span> w1.project_name = p1.name <span class="syn-kw">AND</span> w2.project_name = p2.name
  <span class="syn-kw">RETURN DISTINCT</span> w1, t, s1, w2, s2`,
      violations: [
        {
          rule: 'SIG-DDD-001 · Error',
          target: 'Table "stock_items" written by both Checkout.Service and Warehouse.Service',
          loc: 'src/Checkout.Service/Repositories/StockWriter.cs:59',
        },
      ],
    },
    visitor: {
      file: '.codeexplorer/gates/no-sync-over-async.yaml',
      description: 'Catches blocking .Result or .Wait() calls inside async functions that starve thread pools.',
      status: 'Exit 1 · 2 violations',
      statusOk: false,
      rawYaml: `id: SIG-AST-012
name: No Blocking .Result or .Wait() Inside Async Functions
severity: Error
target: "{b.name}"
message: "Async function '{fn.name}' makes blocking call '{b.name}'"
query: |-
  MATCH (fn:Function {is_async: true})-[:CALLS]->(b:Function)
  WHERE b.name IN ["get_Result", "Wait", "GetResult"] AND NOT fn.is_test
  RETURN fn, b`,
      yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">SIG-AST-012</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Blocking .Result or .Wait() Inside Async Functions</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">target:</span> <span class="syn-str">"{b.name}"</span>
<span class="syn-kw">message:</span> <span class="syn-str">"Async function '{fn.name}' makes blocking call '{b.name}'"</span>
<span class="syn-kw">query:</span> |-
  <span class="syn-kw">MATCH</span> (fn:Function {is_async: <span class="syn-num">true</span>})-[:CALLS]-&gt;(b:Function)
  <span class="syn-kw">WHERE</span> b.name <span class="syn-kw">IN</span> [<span class="syn-str">"get_Result"</span>, <span class="syn-str">"Wait"</span>, <span class="syn-str">"GetResult"</span>] <span class="syn-kw">AND NOT</span> fn.is_test
  <span class="syn-kw">RETURN</span> fn, b`,
      violations: [
        {
          rule: 'SIG-AST-012 · Error',
          target: 'Payments.Api.WebhookController.HandleStripeAsync calls Task.get_Result',
          loc: 'src/Payments.Api/Controllers/WebhookController.cs:112',
        },
        {
          rule: 'SIG-AST-012 · Error',
          target: 'Notifications.Worker.EmailDispatcher.SendBatchAsync calls Task.Wait',
          loc: 'src/Notifications.Worker/EmailDispatcher.cs:67',
        },
      ],
    },
    literal: {
      file: '.codeexplorer/gates/no-hardcoded-urls.yaml',
      description: 'Flags hardcoded internal service URLs or connection strings in non-test functions.',
      status: 'Exit 1 · 1 violation',
      statusOk: false,
      rawYaml: `id: SIG-SEC-003
name: No Hardcoded Internal Service URLs Outside Config
severity: Error
target: "{lit.value}"
message: "Function '{fn.name}' passes hardcoded URL '{lit.value}'"
query: |-
  MATCH (fn:Function)-[:PASSES_LITERAL]->(lit:CallLiteral)
  WHERE lit.value STARTS WITH "http://" AND NOT fn.is_test
  RETURN fn, lit`,
      yamlHtml: `<span class="syn-kw">id:</span> <span class="syn-str">SIG-SEC-003</span>
<span class="syn-kw">name:</span> <span class="syn-str">No Hardcoded Internal Service URLs Outside Config</span>
<span class="syn-kw">severity:</span> <span class="syn-fn">Error</span>
<span class="syn-kw">target:</span> <span class="syn-str">"{lit.value}"</span>
<span class="syn-kw">message:</span> <span class="syn-str">"Function '{fn.name}' passes hardcoded URL '{lit.value}'"</span>
<span class="syn-kw">query:</span> |-
  <span class="syn-kw">MATCH</span> (fn:Function)-[:PASSES_LITERAL]-&gt;(lit:CallLiteral)
  <span class="syn-kw">WHERE</span> lit.value <span class="syn-kw">STARTS WITH</span> <span class="syn-str">"http://"</span>
    <span class="syn-kw">AND NOT</span> fn.is_test
  <span class="syn-kw">RETURN</span> fn, lit`,
      violations: [
        {
          rule: 'SIG-SEC-003 · Error',
          target: 'Shipping.Infrastructure.CarrierClient passes literal "http://carrier-internal.prod:8080/v2"',
          loc: 'src/Shipping.Infrastructure/Clients/CarrierClient.cs:29',
        },
      ],
    },
    ratchet: {
      file: '.codeexplorer/baselines/SIG-CA-B-001.json',
      description: 'Tolerates fingerprinted legacy violations while failing CI on any net-new violation.',
      status: 'Exit 0 · 0 new, 2 tolerated',
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
          rule: 'RATCHET PASS · 0 net-new violations',
          target: '2 existing legacy violations matched SHA-256 baseline fingerprints; 0 new violations introduced in PR.',
          loc: '.codeexplorer/baselines/SIG-CA-B-001.json (verified in 6.4 ms)',
          ok: true,
        },
      ],
    },
  };

  let activeGateKey = 'boundaries';
  const gateTabs = document.querySelector('#gates [role="tablist"]');
  if (gateTabs) {
    wireTablist(gateTabs, (tab) => {
      const key = tab.getAttribute('data-gate-preset') || 'boundaries';
      const preset = gatePresets[key];
      if (!preset) return;
      activeGateKey = key;

      const fileEl = document.getElementById('gateFileLabel');
      const yamlEl = document.getElementById('gateYamlArea');
      const descEl = document.getElementById('gateDescriptionText');
      const statusEl = document.getElementById('gateStatusBadge');
      const listEl = document.getElementById('gateViolationsArea');

      if (fileEl) fileEl.textContent = preset.file;
      if (yamlEl) yamlEl.innerHTML = preset.yamlHtml;
      if (descEl) descEl.textContent = preset.description;
      if (statusEl) {
        statusEl.textContent = preset.status;
        statusEl.className = preset.statusOk ? 'measured ok' : 'measured bad';
      }
      if (listEl) {
        listEl.innerHTML = preset.violations
          .map(
            (v) => `
          <div class="violation">
            <span class="violation-rule ${v.ok ? 'ok' : 'bad'}">${esc(v.rule)}</span>
            <span class="violation-target">${esc(v.target)}</span>
            <span class="violation-loc">${esc(v.loc)}</span>
          </div>`,
          )
          .join('');
      }
    });

    const copyGateBtn = document.querySelector<HTMLElement>('[data-copy-gate]');
    if (copyGateBtn) {
      copyGateBtn.onclick = () => {
        const preset = gatePresets[activeGateKey];
        if (preset) copyText(preset.rawYaml, 'Gate configuration copied');
      };
    }
  }

  // 4. Installation method tabs
  const installTabs = document.querySelector('#install [role="tablist"]');
  if (installTabs) {
    const cmdEl = document.getElementById('install-cmd');
    const noteEl = document.getElementById('install-note');
    wireTablist(installTabs, (tab) => {
      const cmd = tab.getAttribute('data-cmd') || '';
      const note = tab.getAttribute('data-note') || '';
      if (cmdEl) cmdEl.textContent = cmd;
      if (noteEl) noteEl.textContent = note;
    });

    const copyInstallBtn = document.querySelector<HTMLElement>('[data-copy-install]');
    if (copyInstallBtn && cmdEl) {
      copyInstallBtn.onclick = () => {
        copyText(cmdEl.textContent || '', 'Install command copied');
      };
    }
  }

  // 5. Animate benchmark race bars when scrolled into view
  const races = document.querySelectorAll('.page-home .race');
  let observer: IntersectionObserver | null = null;
  if ('IntersectionObserver' in window) {
    observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('in-view');
            observer?.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.25 },
    );
    races.forEach((r) => observer?.observe(r));
  } else {
    races.forEach((r) => r.classList.add('in-view'));
  }

  return () => {
    document.removeEventListener('keydown', onKeyDown);
    if (observer) observer.disconnect();
  };
}
