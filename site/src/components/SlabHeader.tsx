import React, { useEffect, useState } from 'react';

export interface SlabHeaderProps {
  currentRoute: string;
}

const HOME_ROUTE = '/';
const ARCH_ROUTE = '/architectures';
const ONT_ROUTE = '/ontology';
const BENCH_ROUTE = '/benchmarks';
const BENCH_PREFIX = '/benchmarks/';
const DEFAULT_VERSION = 'v1.83.0';
const VERSION_JSON_URL = '/version.json';
const VERSION_PREFIX = 'v';
const FOOTER_VER_ID = 'footerCeVersion';
const NO_CACHE_OPTIONS: RequestInit = { cache: 'no-cache' };

export const SlabHeader: React.FC<SlabHeaderProps> = ({ currentRoute }) => {
  const [releaseVersion, setReleaseVersion] = useState<string>(DEFAULT_VERSION);

  useEffect(() => {
    fetch(VERSION_JSON_URL, NO_CACHE_OPTIONS)
      .then((r) => (r.ok ? r.json() : null))
      .then((data: { tag_name?: string; version?: string } | null) => {
        const rawVer = data && (data.tag_name || data.version);
        if (rawVer) {
          const v = rawVer.startsWith(VERSION_PREFIX) ? rawVer : `${VERSION_PREFIX}${rawVer}`;
          setReleaseVersion(v);
          const footerVer = document.getElementById(FOOTER_VER_ID);
          if (footerVer) {
            footerVer.textContent = `CodeExplorer CLI & Extension ${v}`;
          }
        }
      })
      .catch(() => {
        // Keep static default version when version.json is unavailable
      });
  }, [currentRoute]);

  const isHome = currentRoute === HOME_ROUTE;
  const isArch = currentRoute === ARCH_ROUTE;
  const isOnt = currentRoute === ONT_ROUTE;
  const isBench = currentRoute === BENCH_ROUTE || currentRoute.startsWith(BENCH_PREFIX);

  const queryHref = isHome ? '#playground' : '/#playground';
  const mcpHref = isHome ? '#ai-mcp' : '/#ai-mcp';
  const gatesHref = isHome ? '#gates' : '/#gates';
  const installHref = isHome ? '#install' : '/#install';

  return (
    <header className="slab-header">
      <nav className="wrap slab-bar" aria-label="Primary">
        <a href="/" className="brand-lockup" aria-label="CodeExplorer home">
          <span className="brand-mark">ce</span>
          <span>CodeExplorer</span>
        </a>

        <ul className="slab-links">
          <li className="slab-anchor-item">
            <a href={queryHref}>Query</a>
          </li>
          <li className="slab-anchor-item">
            <a href={mcpHref}>MCP</a>
          </li>
          <li className="slab-anchor-item">
            <a href={gatesHref}>Gates</a>
          </li>
          <li className="slab-anchor-item">
            <a href={installHref}>Install</a>
          </li>
          <li className="slab-sep" aria-hidden="true" />
          <li>
            <a href="/architectures" aria-current={isArch ? 'page' : undefined}>
              Architectures
            </a>
          </li>
          <li>
            <a href="/ontology" aria-current={isOnt ? 'page' : undefined}>
              Ontology
            </a>
          </li>
          <li>
            <a href="/benchmarks" aria-current={isBench ? 'page' : undefined}>
              Benchmarks
            </a>
          </li>
        </ul>

        <div className="slab-actions">
          <a
            href="https://github.com/vmikhailov/code-explorer/releases/latest"
            target="_blank"
            rel="noopener"
            className="btn"
            id="heroReleaseBadge"
            aria-label="Latest CodeExplorer release"
          >
            {releaseVersion}
          </a>
          <a
            href="https://github.com/vmikhailov/code-explorer"
            target="_blank"
            rel="noopener"
            className="btn"
          >
            GitHub
          </a>
        </div>
      </nav>
    </header>
  );
};
