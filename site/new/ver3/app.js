(() => {
  document.documentElement.classList.add('js');

  // ---------- Toast & Clipboard ----------
  const toast = document.getElementById('toast');
  let timer;
  const showToast = (msg) => {
    toast.textContent = msg;
    toast.classList.add('show');
    clearTimeout(timer);
    timer = setTimeout(() => toast.classList.remove('show'), 1800);
  };
  const copyText = async (text, msg = 'Copied to clipboard') => {
    try { await navigator.clipboard.writeText(text); showToast(msg); }
    catch { showToast('Copy failed, select the text instead'); }
  };

  document.querySelectorAll('[data-copy]').forEach((el) =>
    el.addEventListener('click', () => copyText(el.dataset.copy, 'Install command copied'))
  );

  // ---------- Accessible tablist helper ----------
  function wireTablist(list, onSelect) {
    const tabs = [...list.querySelectorAll('[role="tab"]')];
    const select = (tab, focus) => {
      tabs.forEach((t) => {
        const on = t === tab;
        t.setAttribute('aria-selected', String(on));
        t.tabIndex = on ? 0 : -1;
      });
      if (focus) tab.focus();
      onSelect(tab);
    };
    tabs.forEach((tab, i) => {
      tab.addEventListener('click', () => select(tab, false));
      tab.addEventListener('keydown', (e) => {
        const vertical = list.getAttribute('aria-orientation') === 'vertical';
        const next = vertical ? 'ArrowDown' : 'ArrowRight';
        const prev = vertical ? 'ArrowUp' : 'ArrowLeft';
        let j = null;
        if (e.key === next) j = (i + 1) % tabs.length;
        if (e.key === prev) j = (i - 1 + tabs.length) % tabs.length;
        if (e.key === 'Home') j = 0;
        if (e.key === 'End') j = tabs.length - 1;
        if (j !== null) { e.preventDefault(); select(tabs[j], true); }
      });
    });
  }

  // ---------- Hero Plate Switcher ----------
  const plates = {
    orbits: {
      fig: 'Fig. 01',
      title: 'System orbit map of twentyhq/twenty (24,527 files)',
      src: '/all_services.png?v=2',
      alt: 'CodeExplorer orbit map of twentyhq/twenty showing twenty-server surrounded by services, queues and databases on concentric orbits.',
      caption: 'Concentric dependency orbits rendered directly from source code. Blue nodes are services, amber nodes are queues, purple nodes are databases.',
    },
    flow: {
      fig: 'Fig. 02',
      title: 'Project Flow (C2) of bundle-update-adapter',
      src: '/project_flow.png?v=2',
      alt: 'CodeExplorer Project Flow view in VS Code showing bundle-update-adapter connected to PostgreSQL tables, Pub/Sub topics, Redis cache and libraries.',
      caption: 'Service-level flow showing incoming HTTP routes, Pub/Sub topics, Redis cache, PostgreSQL table lineage, and library dependencies.',
    },
  };
  const plateSwitch = document.querySelector('.plate-switch');
  if (plateSwitch) {
    wireTablist(plateSwitch, (tab) => {
      const p = plates[tab.dataset.plateKey];
      if (!p) return;
      document.getElementById('plate-fig').textContent = p.fig;
      document.getElementById('plate-title').textContent = p.title;
      const img = document.getElementById('plate-img');
      img.src = p.src;
      img.alt = p.alt;
      document.getElementById('plate-caption').textContent = p.caption;
    });
  }

  // ---------- Workflows Switcher ----------
  const wfTabs = document.querySelector('.wf-tabs');
  if (wfTabs) {
    wireTablist(wfTabs, (tab) => {
      wfTabs.querySelectorAll('[role="tab"]').forEach((t) => {
        document.getElementById(t.getAttribute('aria-controls')).hidden = t !== tab;
      });
    });
  }

  // ---------- Cypher & Gate Workbench ----------
  const esc = (s) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

  const workbenchData = {
    cypher: [
      {
        label: 'Blast radius',
        file: 'blast_radius.cypher',
        raw: `ce query 'MATCH path = (caller:Method)-[:CALLS*1..4]->(target:Method {name: "ChargeAsync"}) RETURN caller.fullName AS UpstreamCaller, length(path) AS Hops ORDER BY Hops ASC LIMIT 10'`,
        code: `<span class="k">MATCH</span> path = (caller:Method)-[:CALLS*<span class="n">1..4</span>]-&gt;\n      (target:Method {name: <span class="s">"ChargeAsync"</span>})\n<span class="k">RETURN</span> caller.fullName <span class="k">AS</span> UpstreamCaller,\n       <span class="f">length</span>(path) <span class="k">AS</span> Hops\n<span class="k">ORDER BY</span> Hops <span class="k">ASC</span>\n<span class="k">LIMIT</span> <span class="n">10</span>`,
        explain: 'Finds every upstream caller within four hops of PaymentProcessor.ChargeAsync.',
        status: '4 rows in 3.8 ms',
        statusClass: 'ok',
        headers: ['UpstreamCaller', 'Hops'],
        rows: [
          ['Billing.Application.Commands.CaptureOrderHandler.Handle', '1'],
          ['Orders.Api.Controllers.CheckoutController.SubmitOrder', '2'],
          ['Subscriptions.Worker.Consumers.RenewalConsumer.Consume', '2'],
          ['Gateway.Bff.Endpoints.MobileCheckoutEndpoint.Execute', '3'],
        ],
      },
      {
        label: 'SQL table writers',
        file: 'table_lineage.cypher',
        raw: `ce query 'MATCH (s:Service)-[:CONTAINS*]->(m:Method)-[op:READS_FROM|WRITES_TO]->(tbl:SqlTable {name: "customer_ledgers"}) RETURN s.name AS Service, m.name AS Method, type(op) AS Operation'`,
        code: `<span class="k">MATCH</span> (s:Service)-[:CONTAINS*]-&gt;(m:Method)\n      -[op:READS_FROM|WRITES_TO]-&gt;(tbl:SqlTable {name: <span class="s">"customer_ledgers"</span>})\n<span class="k">RETURN</span> s.name <span class="k">AS</span> Service,\n       m.name <span class="k">AS</span> Method,\n       <span class="f">type</span>(op) <span class="k">AS</span> Operation`,
        explain: 'Discovers every microservice and repository method reading or writing a shared table.',
        status: '4 rows in 2.9 ms',
        statusClass: 'ok',
        headers: ['Service', 'Method', 'Operation'],
        rows: [
          ['Billing.Service', 'LedgerRepository.AppendEntryAsync', 'WRITES_TO'],
          ['Billing.Service', 'BalanceQueryService.GetBalanceAsync', 'READS_FROM'],
          ['Reporting.Worker', 'DailyReconciliationJob.ExportLedgers', 'READS_FROM'],
          ['Fraud.Engine', 'RiskScorer.EvaluateRecentVelocity', 'READS_FROM'],
        ],
      },
      {
        label: 'Project cycles',
        file: 'cycles.cypher',
        raw: `ce query 'MATCH (a:Project)-[:DEPENDS_ON]->(b:Project)-[:DEPENDS_ON*1..3]->(a) WHERE a.name < b.name RETURN a.name AS ProjectA, b.name AS ProjectB'`,
        code: `<span class="k">MATCH</span> (a:Project)-[:DEPENDS_ON]-&gt;(b:Project)\n      -[:DEPENDS_ON*<span class="n">1..3</span>]-&gt;(a)\n<span class="k">WHERE</span> a.name &lt; b.name\n<span class="k">RETURN</span> a.name <span class="k">AS</span> ProjectA,\n       b.name <span class="k">AS</span> ProjectB`,
        explain: 'Detects direct and multi-hop circular project dependencies across the workspace.',
        status: '2 rows in 1.9 ms',
        statusClass: 'ok',
        headers: ['ProjectA', 'ProjectB'],
        rows: [
          ['Orders.Application', 'Shared.Fulfillment.Contracts'],
          ['Identity.Core', 'Tenancy.Resolution'],
        ],
      },
    ],
    gates: [
      {
        label: 'Layer boundary',
        file: '.codeexplorer/gates/domain-purity.yaml',
        raw: `id: ARCH-001\nname: Domain Layer Must Not Reference Infrastructure\nseverity: error\nquery: |\n  MATCH (d:Class)-[:DEPENDS_ON|CALLS]->(inf:Class)\n  WHERE d.namespace CONTAINS ".Domain"\n    AND inf.namespace CONTAINS ".Infrastructure"\n  RETURN d.fullName, inf.fullName`,
        code: `<span class="k">id:</span> <span class="s">ARCH-001</span>\n<span class="k">name:</span> <span class="s">Domain Layer Must Not Reference Infrastructure</span>\n<span class="k">severity:</span> <span class="f">error</span>\n<span class="k">query:</span> |\n  <span class="k">MATCH</span> (d:Class)-[:DEPENDS_ON|CALLS]-&gt;(inf:Class)\n  <span class="k">WHERE</span> d.namespace <span class="k">CONTAINS</span> <span class="s">".Domain"</span>\n    <span class="k">AND</span> inf.namespace <span class="k">CONTAINS</span> <span class="s">".Infrastructure"</span>\n  <span class="k">RETURN</span> d.fullName, inf.fullName`,
        explain: 'Blocks Domain types from referencing Infrastructure or ORM assemblies.',
        status: 'Exit 1 (2 violations)',
        statusClass: 'bad',
        violations: [
          { rule: 'ARCH-001', msg: 'Orders.Domain.Aggregates.Order -> Orders.Infrastructure.Persistence.OrderDbContext', loc: 'src/Orders.Domain/Aggregates/Order.cs:84' },
          { rule: 'ARCH-001', msg: 'Billing.Domain.Services.InvoiceCalculator -> Billing.Infrastructure.Http.TaxRateClient', loc: 'src/Billing.Domain/Services/InvoiceCalculator.cs:41' },
        ],
      },
      {
        label: 'Shared SQL writes',
        file: '.codeexplorer/gates/no-shared-database-writes.yaml',
        raw: `id: TOPO-004\nname: Single-Writer Microservice Table Ownership\nseverity: error\nquery: |\n  MATCH (s1:Service)-[:CONTAINS*]->(:Method)-[:WRITES_TO]->(t:SqlTable)<-[:WRITES_TO]-(:Method)<-[:CONTAINS*]-(s2:Service)\n  WHERE s1.name < s2.name\n  RETURN t.name, s1.name, s2.name`,
        code: `<span class="k">id:</span> <span class="s">TOPO-004</span>\n<span class="k">name:</span> <span class="s">Single-Writer Microservice Table Ownership</span>\n<span class="k">severity:</span> <span class="f">error</span>\n<span class="k">query:</span> |\n  <span class="k">MATCH</span> (s1:Service)-[:CONTAINS*]-&gt;(:Method)-[:WRITES_TO]-&gt;(t:SqlTable)\n        &lt;-[:WRITES_TO]-(:Method)&lt;-[:CONTAINS*]-(s2:Service)\n  <span class="k">WHERE</span> s1.name &lt; s2.name\n  <span class="k">RETURN</span> t.name, s1.name, s2.name`,
        explain: 'Forbids two distinct microservices from writing to the same SQL table directly.',
        status: 'Exit 1 (1 violation)',
        statusClass: 'bad',
        violations: [
          { rule: 'TOPO-004', msg: 'Table "inventory.stock_items" written by Checkout.Service and Warehouse.Service', loc: 'src/Checkout.Service/Repositories/StockWriter.cs:59' },
        ],
      },
      {
        label: 'Baseline ratchet',
        file: '.codeexplorer/gate-baseline.json',
        raw: `ce gate check --ratchet`,
        code: `{\n  <span class="k">"schemaVersion"</span>: <span class="n">1</span>,\n  <span class="k">"ratchetMode"</span>: <span class="s">"strict"</span>,\n  <span class="k">"baselinedViolations"</span>: [\n    { <span class="k">"gateId"</span>: <span class="s">"ARCH-001"</span>, <span class="k">"fingerprint"</span>: <span class="s">"sha256:9f84c2e1"</span> },\n    { <span class="k">"gateId"</span>: <span class="s">"TOPO-004"</span>, <span class="k">"fingerprint"</span>: <span class="s">"sha256:4d19b7c8"</span> },\n    { <span class="k">"gateId"</span>: <span class="s">"AST-012"</span>,  <span class="k">"fingerprint"</span>: <span class="s">"sha256:7a31e0f4"</span> }\n  ]\n}`,
        explain: 'Suppresses fingerprinted legacy debt while failing CI immediately on any net-new violation.',
        status: 'Exit 0 (0 new, 3 baselined)',
        statusClass: 'ok',
        violations: [
          { ok: true, rule: 'RATCHET PASS', msg: '3 known legacy violations matched baseline fingerprints; 0 net-new violations in PR.', loc: '.codeexplorer/gate-baseline.json (6.4 ms)' },
        ],
      },
    ],
  };

  let wbMode = 'cypher';
  let wbIndex = 0;

  function renderWorkbenchItem(mode, idx) {
    wbMode = mode;
    wbIndex = idx;
    const item = workbenchData[mode][idx];
    document.getElementById('wb-file').textContent = item.file;
    document.getElementById('wb-code').innerHTML = item.code;
    document.getElementById('wb-explain').textContent = item.explain;
    document.getElementById('wb-out-label').textContent = mode === 'cypher' ? 'Execution result' : 'Gate evaluation';
    const st = document.getElementById('wb-status');
    st.textContent = item.status;
    st.className = `mono ${item.statusClass}`;

    const out = document.getElementById('wb-out');
    if (mode === 'cypher') {
      const th = item.headers.map((h) => `<th>${esc(h)}</th>`).join('');
      const tr = item.rows.map((r) => `<tr>${r.map((c) => `<td>${esc(c)}</td>`).join('')}</tr>`).join('');
      out.innerHTML = `<table class="res-table"><thead><tr>${th}</tr></thead><tbody>${tr}</tbody></table>`;
    } else {
      out.innerHTML = `<div class="viol-list">${item.violations.map((v) =>
        `<div class="viol-item${v.ok ? ' viol-item--ok' : ''}"><span class="viol-rule">${esc(v.rule)}</span><span class="viol-msg">${esc(v.msg)}</span><span class="viol-loc">${esc(v.loc)}</span></div>`
      ).join('')}</div>`;
    }
  }

  function renderPresets(mode) {
    const row = document.getElementById('preset-row');
    row.innerHTML = workbenchData[mode].map((item, i) =>
      `<button type="button" role="tab" class="preset-chip" aria-selected="${i === 0}" tabindex="${i === 0 ? 0 : -1}" data-idx="${i}">${esc(item.label)}</button>`
    ).join('');
    wireTablist(row, (tab) => renderWorkbenchItem(mode, Number(tab.dataset.idx)));
    renderWorkbenchItem(mode, 0);
  }

  const modeSwitch = document.querySelector('.mode-switch');
  if (modeSwitch) {
    wireTablist(modeSwitch, (tab) => renderPresets(tab.dataset.mode));
    renderPresets('cypher');
    document.getElementById('wb-copy').addEventListener('click', () => {
      const item = workbenchData[wbMode][wbIndex];
      copyText(item.raw, wbMode === 'cypher' ? 'CLI command copied' : 'Gate YAML copied');
    });
  }

  // ---------- Install Tabs ----------
  const installTabs = document.querySelector('.install-tabs');
  if (installTabs) {
    const cmdEl = document.getElementById('install-cmd');
    const noteEl = document.getElementById('install-note');
    wireTablist(installTabs, (tab) => {
      cmdEl.textContent = tab.dataset.cmd;
      noteEl.textContent = tab.dataset.note;
    });
    document.getElementById('copy-install').addEventListener('click', () =>
      copyText(cmdEl.textContent, 'Install command copied')
    );
  }

  // ---------- Benchmark Bars Reveal ----------
  const rows = document.querySelectorAll('.ledger-row');
  if ('IntersectionObserver' in window) {
    const io = new IntersectionObserver((entries) => {
      entries.forEach((e) => {
        if (e.isIntersecting) { e.target.classList.add('in-view'); io.unobserve(e.target); }
      });
    }, { threshold: 0.25 });
    rows.forEach((r) => io.observe(r));
  } else {
    rows.forEach((r) => r.classList.add('in-view'));
  }
})();
