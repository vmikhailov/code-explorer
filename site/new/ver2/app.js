(() => {
  // ---------- toast + clipboard ----------
  const toast = document.getElementById('toast');
  let timer;
  const show = (msg) => {
    toast.textContent = msg;
    toast.classList.add('show');
    clearTimeout(timer);
    timer = setTimeout(() => toast.classList.remove('show'), 1800);
  };
  const copy = async (text) => {
    try { await navigator.clipboard.writeText(text); show('Copied'); }
    catch { show('Copy failed, select the text instead'); }
  };
  document.querySelectorAll('[data-copy]').forEach((el) => el.addEventListener('click', () => copy(el.dataset.copy)));

  // ---------- install method tabs ----------
  const list = document.querySelector('.prepare .chips');
  const cmd = document.getElementById('install-cmd');
  if (list && cmd) {
    const tabs = [...list.querySelectorAll('[role="tab"]')];
    const select = (tab, focus) => {
      tabs.forEach((t) => { const on = t === tab; t.setAttribute('aria-selected', String(on)); t.tabIndex = on ? 0 : -1; });
      cmd.textContent = tab.dataset.cmd;
      if (focus) tab.focus();
    };
    tabs.forEach((tab, i) => {
      tab.addEventListener('click', () => select(tab, false));
      tab.addEventListener('keydown', (e) => {
        let j = null;
        if (e.key === 'ArrowRight') j = (i + 1) % tabs.length;
        if (e.key === 'ArrowLeft') j = (i - 1 + tabs.length) % tabs.length;
        if (e.key === 'Home') j = 0;
        if (e.key === 'End') j = tabs.length - 1;
        if (j !== null) { e.preventDefault(); select(tabs[j], true); }
      });
    });
    document.querySelector('[data-copy-install]').addEventListener('click', () => copy(cmd.textContent));
  }

  // ---------- decorative barcode, derived from the version string ----------
  const svg = document.getElementById('barcode');
  if (svg) {
    const seed = 'CE-1.97.0-MIT';
    const widths = [];
    for (const ch of seed) {
      const c = ch.charCodeAt(0);
      widths.push(1 + (c % 3), 1 + ((c >> 2) % 2), 1 + ((c >> 1) % 3), 1 + ((c >> 3) % 2));
    }
    const unit = 190 / widths.reduce((a, b) => a + b, 4);
    let x = unit * 2;
    const ns = 'http://www.w3.org/2000/svg';
    widths.forEach((w, i) => {
      if (i % 2 === 0) {
        const r = document.createElementNS(ns, 'rect');
        r.setAttribute('x', x.toFixed(2));
        r.setAttribute('y', '0');
        r.setAttribute('width', (w * unit).toFixed(2));
        r.setAttribute('height', i % 14 === 0 ? '70' : '62');
        svg.appendChild(r);
      }
      x += w * unit;
    });
  }
})();
