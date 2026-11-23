(() => {
  document.documentElement.classList.add('js');

  // ---------- toast + clipboard ----------
  const toast = document.getElementById('toast');
  let toastTimer;
  function showToast(msg) {
    toast.textContent = msg;
    toast.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => toast.classList.remove('show'), 1800);
  }
  async function copy(text, msg = 'Copied') {
    try {
      await navigator.clipboard.writeText(text);
      showToast(msg);
    } catch {
      showToast('Copy failed, select the text instead');
    }
  }

  document.querySelectorAll('[data-copy]').forEach((el) =>
    el.addEventListener('click', () => copy(el.dataset.copy, 'Install command copied'))
  );

  // ---------- nav border on scroll ----------
  const nav = document.querySelector('.nav');
  const onScroll = () => nav.classList.toggle('is-scrolled', window.scrollY > 8);
  window.addEventListener('scroll', onScroll, { passive: true });
  onScroll();

  // ---------- accessible tablists (arrow keys, roving tabindex) ----------
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

  // readers
  const readerList = document.querySelector('.reader-list');
  if (readerList) {
    wireTablist(readerList, (tab) => {
      readerList.querySelectorAll('[role="tab"]').forEach((t) => {
        document.getElementById(t.getAttribute('aria-controls')).hidden = t !== tab;
      });
    });
  }

  // ---------- query presets ----------
  const presets = {
    blast_radius: {
      file: 'blast_radius.cypher',
      raw: 'MATCH path = (caller:Method)-[:CALLS*1..4]->(target:Method {name: "ChargeAsync"}) RETURN caller.fullName AS UpstreamCaller, length(path) AS Hops ORDER BY Hops ASC LIMIT 10',
      html: `<span class="k">MATCH</span> path = (caller:Method)-[:CALLS*<span class="n">1..4</span>]-&gt;
      (target:Method {name: <span class="s">"ChargeAsync"</span>})
<span class="k">RETURN</span> caller.fullName <span class="k">AS</span> UpstreamCaller,
       <span class="f">length</span>(path) <span class="k">AS</span> Hops
<span class="k">ORDER BY</span> Hops <span class="k">ASC</span>
<span class="k">LIMIT</span> <span class="n">10</span>`,
      explain: 'Every upstream caller within four hops of ChargeAsync.',
      time: '4 rows in 3.8 ms',
      headers: ['UpstreamCaller', 'Hops'],
      rows: [
        ['Billing.Application.Commands.CaptureOrderHandler.Handle', '1'],
        ['Orders.Api.Controllers.CheckoutController.SubmitOrder', '2'],
        ['Subscriptions.Worker.Consumers.RenewalConsumer.Consume', '2'],
        ['Gateway.Bff.Endpoints.MobileCheckoutEndpoint.Execute', '3'],
      ],
    },
    api_to_db: {
      file: 'table_writers.cypher',
      raw: 'MATCH (s:Service)-[:CONTAINS*]->(m:Method)-[op:READS_FROM|WRITES_TO]->(tbl:SqlTable {name: "customer_ledgers"}) RETURN s.name AS Service, m.name AS Method, type(op) AS Operation',
      html: `<span class="k">MATCH</span> (s:Service)-[:CONTAINS*]-&gt;(m:Method)
      -[op:READS_FROM|WRITES_TO]-&gt;(tbl:SqlTable {name: <span class="s">"customer_ledgers"</span>})
<span class="k">RETURN</span> s.name <span class="k">AS</span> Service,
       m.name <span class="k">AS</span> Method,
       <span class="f">type</span>(op) <span class="k">AS</span> Operation`,
      explain: 'Every service and method that reads or writes a shared table.',
      time: '4 rows in 2.9 ms',
      headers: ['Service', 'Method', 'Operation'],
      rows: [
        ['Billing.Service', 'LedgerRepository.AppendEntryAsync', 'WRITES_TO'],
        ['Billing.Service', 'BalanceQueryService.GetBalanceAsync', 'READS_FROM'],
        ['Reporting.Worker', 'DailyReconciliationJob.ExportLedgers', 'READS_FROM'],
        ['Fraud.Engine', 'RiskScorer.EvaluateRecentVelocity', 'READS_FROM'],
      ],
    },
    cqrs_pipeline: {
      file: 'endpoint_to_table.cypher',
      raw: 'MATCH (ep:Endpoint)-[:CALLS*1..2]->(cmd:Class)-[:HANDLED_BY]->(h:Class)-[:WRITES_TO]->(t:SqlTable) RETURN ep.route AS Route, cmd.name AS Command, h.name AS Handler, t.name AS Table',
      html: `<span class="k">MATCH</span> (ep:Endpoint)-[:CALLS*<span class="n">1..2</span>]-&gt;(cmd:Class)
      -[:HANDLED_BY]-&gt;(h:Class)-[:WRITES_TO]-&gt;(t:SqlTable)
<span class="k">RETURN</span> ep.route <span class="k">AS</span> Route,
       cmd.name <span class="k">AS</span> Command,
       t.name <span class="k">AS</span> Table`,
      explain: 'HTTP endpoints, through commands and handlers, to the tables they change.',
      time: '3 rows in 4.2 ms',
      headers: ['Route', 'Command', 'Table'],
      rows: [
        ['POST /api/v1/orders', 'CreateOrderCommand', 'orders.purchase_orders'],
        ['POST /api/v1/refunds', 'IssueRefundCommand', 'billing.ledger_entries'],
        ['PUT /api/v1/inventory/reserve', 'ReserveStockCommand', 'warehouse.stock_reservations'],
      ],
    },
    cyclic_deps: {
      file: 'cycles.cypher',
      raw: 'MATCH (a:Project)-[:DEPENDS_ON]->(b:Project)-[:DEPENDS_ON*1..3]->(a) WHERE a.name < b.name RETURN a.name AS ProjectA, b.name AS ProjectB',
      html: `<span class="k">MATCH</span> (a:Project)-[:DEPENDS_ON]-&gt;(b:Project)
      -[:DEPENDS_ON*<span class="n">1..3</span>]-&gt;(a)
<span class="k">WHERE</span> a.name &lt; b.name
<span class="k">RETURN</span> a.name <span class="k">AS</span> ProjectA,
       b.name <span class="k">AS</span> ProjectB`,
      explain: 'Direct and multi-hop circular dependencies between projects.',
      time: '2 rows in 1.9 ms',
      headers: ['ProjectA', 'ProjectB'],
      rows: [
        ['Orders.Application', 'Shared.Fulfillment.Contracts'],
        ['Identity.Core', 'Tenancy.Resolution'],
      ],
    },
  };
  let activePreset = 'blast_radius';
  const esc = (s) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

  const queryTabs = document.querySelector('[data-query] .chips');
  if (queryTabs) {
    wireTablist(queryTabs, (tab) => {
      const p = presets[tab.dataset.preset];
      if (!p) return;
      activePreset = tab.dataset.preset;
      document.getElementById('q-file').textContent = p.file;
      document.getElementById('q-code').innerHTML = p.html;
      document.getElementById('q-explain').textContent = p.explain;
      document.getElementById('q-time').textContent = p.time;
      const th = p.headers.map((h) => `<th>${esc(h)}</th>`).join('');
      const tr = p.rows.map((r) => `<tr>${r.map((c) => `<td>${esc(c)}</td>`).join('')}</tr>`).join('');
      document.getElementById('q-result').innerHTML = `<table class="table"><thead><tr>${th}</tr></thead><tbody>${tr}</tbody></table>`;
    });
    document.querySelector('[data-copy-query]').addEventListener('click', () =>
      copy(`ce query '${presets[activePreset].raw}'`, 'CLI command copied')
    );
  }

  // ---------- install methods ----------
  const installTabs = document.querySelector('[data-install] .chips');
  if (installTabs) {
    const cmdEl = document.getElementById('install-cmd');
    const noteEl = document.getElementById('install-note');
    wireTablist(installTabs, (tab) => {
      cmdEl.textContent = tab.dataset.cmd;
      noteEl.textContent = tab.dataset.note;
    });
    document.querySelector('[data-copy-install]').addEventListener('click', () =>
      copy(cmdEl.textContent, 'Install command copied')
    );
  }

  // ---------- benchmark bars grow when seen ----------
  const races = document.querySelectorAll('.race');
  if ('IntersectionObserver' in window) {
    const io = new IntersectionObserver((entries) => {
      entries.forEach((e) => {
        if (e.isIntersecting) { e.target.classList.add('in-view'); io.unobserve(e.target); }
      });
    }, { threshold: 0.35 });
    races.forEach((r) => io.observe(r));
  } else {
    races.forEach((r) => r.classList.add('in-view'));
  }
})();
