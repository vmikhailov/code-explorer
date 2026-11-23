(() => {
  // ---------- Toast & Clipboard ----------
  const toast = document.getElementById('toast');
  let toastTimer;
  const showToast = (msg) => {
    toast.textContent = msg;
    toast.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toast.classList.remove('show'), 1800);
  };
  const copyText = async (text, msg = 'Copied to clipboard') => {
    try { await navigator.clipboard.writeText(text); showToast(msg); }
    catch { showToast('Select text to copy'); }
  };

  document.querySelectorAll('[data-copy]').forEach((el) =>
    el.addEventListener('click', () => copyText(el.dataset.copy, 'Install command copied'))
  );

  // ---------- Accessible Tablist Helper ----------
  function wireTablist(container, onSelect) {
    const tabs = [...container.querySelectorAll('[role="tab"]')];
    const activate = (tab, focus) => {
      tabs.forEach((t) => {
        const active = t === tab;
        t.setAttribute('aria-selected', String(active));
        t.tabIndex = active ? 0 : -1;
      });
      if (focus) tab.focus();
      onSelect(tab);
    };
    tabs.forEach((tab, i) => {
      tab.addEventListener('click', () => activate(tab, false));
      tab.addEventListener('keydown', (e) => {
        const vert = container.getAttribute('aria-orientation') === 'vertical';
        const next = vert ? 'ArrowDown' : 'ArrowRight';
        const prev = vert ? 'ArrowUp' : 'ArrowLeft';
        let idx = null;
        if (e.key === next) idx = (i + 1) % tabs.length;
        if (e.key === prev) idx = (i - 1 + tabs.length) % tabs.length;
        if (e.key === 'Home') idx = 0;
        if (e.key === 'End') idx = tabs.length - 1;
        if (idx !== null) { e.preventDefault(); activate(tabs[idx], true); }
      });
    });
  }

  // ---------- Hero Studio Stage Tabs ----------
  const studioTabs = document.querySelector('.studio-tabs');
  if (studioTabs) {
    wireTablist(studioTabs, (tab) => {
      const target = tab.dataset.stage;
      ['interactive', 'orbits', 'flow'].forEach((id) => {
        document.getElementById(`stage-${id}`).hidden = id !== target;
      });
    });
  }

  // ---------- Live Interactive Topology Graph ----------
  const nodeCatalog = {
    gateway: {
      kind: 'Layer 4 · BFF Service',
      name: 'api-gateway',
      ms: '1.8 ms',
      summary: 'TypeScript edge gateway authenticating client sessions and routing checkout and billing mutations.',
      mcp: 'get_call_chain(symbol: "CheckoutRoute", depth: 3)',
    },
    orders: {
      kind: 'Layer 4 · Core Workload',
      name: 'orders-service',
      ms: '2.4 ms',
      summary: 'Core C# order orchestration service. Receives checkout traffic, invokes billing, emits domain events, and owns purchase_orders.',
      mcp: 'analyze_code_impact("orders-service")',
    },
    billing: {
      kind: 'Layer 4 · Payment Workload',
      name: 'billing-service',
      ms: '3.1 ms',
      summary: 'Executes ChargeAsync, appends immutable entries to customer_ledgers, and dispatches outbound Stripe webhooks.',
      mcp: 'get_callers(symbol: "ChargeAsync", depth: 4)',
    },
    fraud: {
      kind: 'Layer 4 · Async Worker',
      name: 'fraud-engine',
      ms: '2.2 ms',
      summary: 'Python velocity scorer consuming orders.events and cross-checking customer_ledgers and purchase_orders.',
      mcp: 'get_service_contracts("fraud-engine")',
    },
    q_orders: {
      kind: 'Layer 4 · Kafka Topic',
      name: 'orders.events',
      ms: '1.5 ms',
      summary: 'Order lifecycle event stream published by orders-service and consumed by billing-service and fraud-engine.',
      mcp: 'execute_custom_read_cypher("MATCH (p)-[:PUBLISHES|CONSUMES]->(t {name:\\"orders.events\\"})")',
    },
    q_webhooks: {
      kind: 'Layer 4 · RabbitMQ Queue',
      name: 'stripe.webhooks',
      ms: '1.4 ms',
      summary: 'Asynchronous outbound settlement notifications emitted after ledger commits.',
      mcp: 'find_symbol("stripe.webhooks")',
    },
    sessions: {
      kind: 'Layer 4 · Redis Cache',
      name: 'redis.sessions',
      ms: '1.1 ms',
      summary: 'Low-latency token and rate-limit cache read by api-gateway on every inbound request.',
      mcp: 'inspect_data_lineage("redis.sessions")',
    },
    db_orders: {
      kind: 'Layer 4 · PostgreSQL Table',
      name: 'purchase_orders',
      ms: '2.7 ms',
      summary: 'Single-writer transactional order table mutated by orders-service and read by fraud-engine.',
      mcp: 'inspect_data_lineage("orders.purchase_orders")',
    },
    db_ledger: {
      kind: 'Layer 4 · PostgreSQL Table',
      name: 'customer_ledgers',
      ms: '2.9 ms',
      summary: 'Append-only financial ledger written by billing-service and audited by fraud-engine.',
      mcp: 'inspect_data_lineage("billing.customer_ledgers")',
    },
  };

  const edges = [...document.querySelectorAll('#graphEdges .g-edge')];
  const nodes = [...document.querySelectorAll('#graphNodes .g-node')];

  function selectGraphNode(id) {
    const info = nodeCatalog[id];
    if (!info) return;

    const neighbors = new Set([id]);
    const connected = [];

    edges.forEach((edge) => {
      const f = edge.dataset.from;
      const t = edge.dataset.to;
      const rel = edge.dataset.label;
      if (f === id || t === id) {
        edge.classList.add('is-active');
        edge.classList.remove('is-dim');
        const peer = f === id ? t : f;
        neighbors.add(peer);
        connected.push({
          rel: f === id ? `→ ${rel}` : `← ${rel}`,
          peer: nodeCatalog[peer].name,
        });
      } else {
        edge.classList.remove('is-active');
        edge.classList.add('is-dim');
      }
    });

    nodes.forEach((n) => {
      const nid = n.dataset.id;
      n.classList.toggle('is-selected', nid === id);
      n.classList.toggle('is-neighbor', nid !== id && neighbors.has(nid));
      n.classList.toggle('is-dim', !neighbors.has(nid));
    });

    document.getElementById('inspKind').textContent = info.kind;
    document.getElementById('inspName').textContent = info.name;
    document.getElementById('inspMs').textContent = info.ms;
    document.getElementById('inspSummary').textContent = info.summary;
    document.getElementById('inspMcp').textContent = info.mcp;
    document.getElementById('inspEdges').innerHTML = connected
      .map((c) => `<li><span class="edge-rel">${c.rel}</span><span class="edge-peer">${c.peer}</span></li>`)
      .join('');
  }

  nodes.forEach((node) => {
    const trigger = () => selectGraphNode(node.dataset.id);
    node.addEventListener('click', trigger);
    node.addEventListener('mouseenter', trigger);
    node.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); trigger(); }
    });
  });
  selectGraphNode('orders');

  // ---------- Lightbox ----------
  const lightbox = document.getElementById('lightbox');
  const lightboxImg = document.getElementById('lightboxImg');
  document.querySelectorAll('[data-zoom]').forEach((btn) => {
    btn.addEventListener('click', () => {
      lightboxImg.src = btn.dataset.zoom;
      lightbox.hidden = false;
    });
  });
  document.getElementById('lightboxClose').addEventListener('click', () => { lightbox.hidden = true; });
  lightbox.addEventListener('click', (e) => { if (e.target === lightbox) lightbox.hidden = true; });
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape') lightbox.hidden = true; });

  // ---------- IDE Cypher & Gate Workbench ----------
  const esc = (s) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

  const wbCatalog = {
    cypher: [
      {
        label: 'Blast Radius (*1..4)',
        file: 'blast_radius.cypher',
        raw: `ce query 'MATCH path = (caller:Method)-[:CALLS*1..4]->(target:Method {name: "ChargeAsync"}) RETURN caller.fullName AS UpstreamCaller, length(path) AS Hops ORDER BY Hops ASC LIMIT 10'`,
        code: `<span class="k">MATCH</span> path = (caller:Method)-[:CALLS*<span class="n">1..4</span>]-&gt;\n      (target:Method {name: <span class="s">"ChargeAsync"</span>})\n<span class="k">RETURN</span> caller.fullName <span class="k">AS</span> UpstreamCaller,\n       <span class="f">length</span>(path) <span class="k">AS</span> Hops\n<span class="k">ORDER BY</span> Hops <span class="k">ASC</span>\n<span class="k">LIMIT</span> <span class="n">10</span>`,
        explain: 'Finds all upstream callers within 4 hops of PaymentProcessor.ChargeAsync.',
        badge: '4 rows · 3.8 ms',
        badgeColor: 'var(--emerald)',
        headers: ['UpstreamCaller', 'Hops'],
        rows: [
          ['Billing.Application.Commands.CaptureOrderHandler.Handle', '1'],
          ['Orders.Api.Controllers.CheckoutController.SubmitOrder', '2'],
          ['Subscriptions.Worker.Consumers.RenewalConsumer.Consume', '2'],
          ['Gateway.Bff.Endpoints.MobileCheckoutEndpoint.Execute', '3'],
        ],
      },
      {
        label: 'Endpoint to SQL Lineage',
        file: 'table_lineage.cypher',
        raw: `ce query 'MATCH (s:Service)-[:CONTAINS*]->(m:Method)-[op:READS_FROM|WRITES_TO]->(tbl:SqlTable {name: "customer_ledgers"}) RETURN s.name AS Service, m.name AS Method, type(op) AS Operation'`,
        code: `<span class="k">MATCH</span> (s:Service)-[:CONTAINS*]-&gt;(m:Method)\n      -[op:READS_FROM|WRITES_TO]-&gt;(tbl:SqlTable {name: <span class="s">"customer_ledgers"</span>})\n<span class="k">RETURN</span> s.name <span class="k">AS</span> Service,\n       m.name <span class="k">AS</span> Method,\n       <span class="f">type</span>(op) <span class="k">AS</span> Operation`,
        explain: 'Lists every microservice and repository method reading or writing customer_ledgers.',
        badge: '4 rows · 2.9 ms',
        badgeColor: 'var(--emerald)',
        headers: ['Service', 'Method', 'Operation'],
        rows: [
          ['Billing.Service', 'LedgerRepository.AppendEntryAsync', 'WRITES_TO'],
          ['Billing.Service', 'BalanceQueryService.GetBalanceAsync', 'READS_FROM'],
          ['Reporting.Worker', 'DailyReconciliationJob.ExportLedgers', 'READS_FROM'],
          ['Fraud.Engine', 'RiskScorer.EvaluateRecentVelocity', 'READS_FROM'],
        ],
      },
      {
        label: 'Cross-Module Cycles',
        file: 'cyclic_deps.cypher',
        raw: `ce query 'MATCH (a:Project)-[:DEPENDS_ON]->(b:Project)-[:DEPENDS_ON*1..3]->(a) WHERE a.name < b.name RETURN a.name AS ProjectA, b.name AS ProjectB'`,
        code: `<span class="k">MATCH</span> (a:Project)-[:DEPENDS_ON]-&gt;(b:Project)\n      -[:DEPENDS_ON*<span class="n">1..3</span>]-&gt;(a)\n<span class="k">WHERE</span> a.name &lt; b.name\n<span class="k">RETURN</span> a.name <span class="k">AS</span> ProjectA,\n       b.name <span class="k">AS</span> ProjectB`,
        explain: 'Detects direct and multi-hop circular project dependencies across the solution.',
        badge: '2 rows · 1.9 ms',
        badgeColor: 'var(--emerald)',
        headers: ['ProjectA', 'ProjectB'],
        rows: [
          ['Orders.Application', 'Shared.Fulfillment.Contracts'],
          ['Identity.Core', 'Tenancy.Resolution'],
        ],
      },
    ],
    gates: [
      {
        label: 'Clean Architecture',
        file: '.codeexplorer/gates/domain-purity.yaml',
        raw: `id: ARCH-001\nname: Domain Layer Must Not Reference Infrastructure\nseverity: error\nquery: |\n  MATCH (d:Class)-[:DEPENDS_ON|CALLS]->(inf:Class)\n  WHERE d.namespace CONTAINS ".Domain"\n    AND inf.namespace CONTAINS ".Infrastructure"\n  RETURN d.fullName, inf.fullName`,
        code: `<span class="k">id:</span> <span class="s">ARCH-001</span>\n<span class="k">name:</span> <span class="s">Domain Layer Must Not Reference Infrastructure</span>\n<span class="k">severity:</span> <span class="f">error</span>\n<span class="k">query:</span> |\n  <span class="k">MATCH</span> (d:Class)-[:DEPENDS_ON|CALLS]-&gt;(inf:Class)\n  <span class="k">WHERE</span> d.namespace <span class="k">CONTAINS</span> <span class="s">".Domain"</span>\n    <span class="k">AND</span> inf.namespace <span class="k">CONTAINS</span> <span class="s">".Infrastructure"</span>\n  <span class="k">RETURN</span> d.fullName, inf.fullName`,
        explain: 'Forbids Domain types from referencing Infrastructure or ORM persistence types.',
        badge: 'Exit 1 · 2 violations',
        badgeColor: 'var(--danger)',
        violations: [
          { id: 'ARCH-001 (error)', msg: 'Orders.Domain.Aggregates.Order -> Orders.Infrastructure.Persistence.OrderDbContext', loc: 'src/Orders.Domain/Aggregates/Order.cs:84' },
          { id: 'ARCH-001 (error)', msg: 'Billing.Domain.Services.InvoiceCalculator -> Billing.Infrastructure.Http.TaxRateClient', loc: 'src/Billing.Domain/Services/InvoiceCalculator.cs:41' },
        ],
      },
      {
        label: 'Single-Writer Table',
        file: '.codeexplorer/gates/table-ownership.yaml',
        raw: `id: TOPO-004\nname: Single-Writer Microservice Table Ownership\nseverity: error\nquery: |\n  MATCH (s1:Service)-[:CONTAINS*]->(:Method)-[:WRITES_TO]->(t:SqlTable)<-[:WRITES_TO]-(:Method)<-[:CONTAINS*]-(s2:Service)\n  WHERE s1.name < s2.name\n  RETURN t.name, s1.name, s2.name`,
        code: `<span class="k">id:</span> <span class="s">TOPO-004</span>\n<span class="k">name:</span> <span class="s">Single-Writer Microservice Table Ownership</span>\n<span class="k">severity:</span> <span class="f">error</span>\n<span class="k">query:</span> |\n  <span class="k">MATCH</span> (s1:Service)-[:CONTAINS*]-&gt;(:Method)-[:WRITES_TO]-&gt;(t:SqlTable)\n        &lt;-[:WRITES_TO]-(:Method)&lt;-[:CONTAINS*]-(s2:Service)\n  <span class="k">WHERE</span> s1.name &lt; s2.name\n  <span class="k">RETURN</span> t.name, s1.name, s2.name`,
        explain: 'Blocks two distinct microservices from writing directly to the same SQL table.',
        badge: 'Exit 1 · 1 violation',
        badgeColor: 'var(--danger)',
        violations: [
          { id: 'TOPO-004 (error)', msg: 'Table "inventory.stock_items" written by Checkout.Service and Warehouse.Service', loc: 'src/Checkout.Service/Repositories/StockWriter.cs:59' },
        ],
      },
      {
        label: 'Baseline Ratchet',
        file: '.codeexplorer/gate-baseline.json',
        raw: `ce gate check --ratchet`,
        code: `{\n  <span class="k">"schemaVersion"</span>: <span class="n">1</span>,\n  <span class="k">"ratchetMode"</span>: <span class="s">"strict"</span>,\n  <span class="k">"baselinedViolations"</span>: [\n    { <span class="k">"gateId"</span>: <span class="s">"ARCH-001"</span>, <span class="k">"fingerprint"</span>: <span class="s">"sha256:9f84c2e1"</span> },\n    { <span class="k">"gateId"</span>: <span class="s">"TOPO-004"</span>, <span class="k">"fingerprint"</span>: <span class="s">"sha256:4d19b7c8"</span> },\n    { <span class="k">"gateId"</span>: <span class="s">"AST-012"</span>,  <span class="k">"fingerprint"</span>: <span class="s">"sha256:7a31e0f4"</span> }\n  ]\n}`,
        explain: 'Suppresses fingerprinted legacy debt while failing CI on any net-new violation.',
        badge: 'Exit 0 · 0 net-new',
        badgeColor: 'var(--emerald)',
        violations: [
          { ok: true, id: 'RATCHET PASS', msg: '3 legacy violations matched baseline hashes; 0 net-new violations introduced in PR.', loc: '.codeexplorer/gate-baseline.json · verified in 6.4 ms' },
        ],
      },
    ],
  };

  let curMode = 'cypher';
  let curIdx = 0;

  function renderWb(mode, idx) {
    curMode = mode;
    curIdx = idx;
    const item = wbCatalog[mode][idx];
    document.getElementById('wbFile').textContent = item.file;
    document.getElementById('wbCode').innerHTML = item.code;
    const lineCount = item.code.split('\n').length;
    document.getElementById('wbLines').innerHTML = Array.from({ length: lineCount }, (_, i) => i + 1).join('<br>');
    document.getElementById('wbExplain').textContent = item.explain;
    document.getElementById('wbOutTitle').textContent = mode === 'cypher' ? 'SQLite Graph Execution' : 'CI Gate Evaluation';
    const badge = document.getElementById('wbBadge');
    badge.textContent = item.badge;
    badge.style.color = item.badgeColor;

    const out = document.getElementById('wbOut');
    if (mode === 'cypher') {
      const th = item.headers.map((h) => `<th>${esc(h)}</th>`).join('');
      const tr = item.rows.map((r) => `<tr>${r.map((c) => `<td>${esc(c)}</td>`).join('')}</tr>`).join('');
      out.innerHTML = `<table class="ide-table"><thead><tr>${th}</tr></thead><tbody>${tr}</tbody></table>`;
    } else {
      out.innerHTML = `<div class="gate-items">${item.violations.map((v) =>
        `<div class="gate-item${v.ok ? ' gate-item--ok' : ''}"><span class="gate-id">${esc(v.id)}</span><span class="gate-msg">${esc(v.msg)}</span><span class="gate-loc">${esc(v.loc)}</span></div>`
      ).join('')}</div>`;
    }
  }

  function renderWbPresets(mode) {
    const wrap = document.getElementById('wbPresets');
    wrap.innerHTML = wbCatalog[mode].map((p, i) =>
      `<button type="button" role="tab" class="p-chip" aria-selected="${i === 0}" tabindex="${i === 0 ? 0 : -1}" data-idx="${i}">${esc(p.label)}</button>`
    ).join('');
    wireTablist(wrap, (tab) => renderWb(mode, Number(tab.dataset.idx)));
    renderWb(mode, 0);
  }

  const modeGroup = document.querySelector('#workbench .seg-group');
  if (modeGroup) {
    wireTablist(modeGroup, (tab) => renderWbPresets(tab.dataset.wbMode));
    renderWbPresets('cypher');
    document.getElementById('wbCopy').addEventListener('click', () => {
      const item = wbCatalog[curMode][curIdx];
      copyText(item.raw, curMode === 'cypher' ? 'CLI query copied' : 'Gate YAML copied');
    });
  }

  // ---------- 5-Layer Stack Interactive Highlight ----------
  const layerStack = document.querySelector('.layer-stack');
  if (layerStack) {
    wireTablist(layerStack, () => {});
  }

  // ---------- Install Switcher ----------
  const instTabs = document.querySelector('.inst-tabs');
  if (instTabs) {
    const cmd = document.getElementById('instCmd');
    const note = document.getElementById('instNote');
    wireTablist(instTabs, (tab) => {
      cmd.textContent = tab.dataset.cmd;
      note.textContent = tab.dataset.note;
    });
    document.getElementById('instCopy').addEventListener('click', () =>
      copyText(cmd.textContent, 'Install command copied')
    );
  }
})();
