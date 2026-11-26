import React, { useEffect, useState } from 'react';

export interface SlabHeaderProps {
  currentRoute: string;
}

const HOME_ROUTE = '/';
const ARCH_ROUTE = '/architectures';
const ONT_ROUTE = '/ontology';
const BENCH_ROUTE = '/benchmarks';
const BENCH_PREFIX = '/benchmarks/';
const DEFAULT_VERSION = 'v1.105.3';
const VERSION_JSON_URL = '/version.json';
const VERSION_PREFIX = 'v';
const FOOTER_VER_ID = 'footerCeVersion';
const NO_CACHE_OPTIONS: RequestInit = { cache: 'no-cache' };

function parseSemVerTuple(ver: string): [number, number, number] {
  const clean = ver.startsWith(VERSION_PREFIX) ? ver.slice(1) : ver;
  const parts = clean.split('.').map((n) => parseInt(n, 10) || 0);
  return [parts[0] || 0, parts[1] || 0, parts[2] || 0];
}

function isNewerOrEqualSemVer(candidate: string, baseline: string): boolean {
  const [cMaj, cMin, cPat] = parseSemVerTuple(candidate);
  const [bMaj, bMin, bPat] = parseSemVerTuple(baseline);
  if (cMaj !== bMaj) return cMaj > bMaj;
  if (cMin !== bMin) return cMin > bMin;
  return cPat >= bPat;
}

export const SlabHeader: React.FC<SlabHeaderProps> = ({ currentRoute }) => {
  const [releaseVersion, setReleaseVersion] = useState<string>(DEFAULT_VERSION);
  const [isScrolled, setIsScrolled] = useState<boolean>(false);

  useEffect(() => {
    const onScroll = (): void => {
      setIsScrolled(window.scrollY > 8);
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  useEffect(() => {
    fetch(VERSION_JSON_URL, NO_CACHE_OPTIONS)
      .then((r) => (r.ok ? r.json() : null))
      .then((data: { tag_name?: string; version?: string } | null) => {
        const rawVer = data && (data.tag_name || data.version);
        if (rawVer) {
          const v = rawVer.startsWith(VERSION_PREFIX) ? rawVer : `${VERSION_PREFIX}${rawVer}`;
          if (isNewerOrEqualSemVer(v, DEFAULT_VERSION)) {
            setReleaseVersion(v);
            const footerVer = document.getElementById(FOOTER_VER_ID);
            if (footerVer) {
              footerVer.textContent = v;
            }
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

  const readersHref = isHome ? '#readers' : '/#readers';
  const queryHref = isHome ? '#playground' : '/#playground';
  const gatesHref = isHome ? '#gates' : '/#gates';
  const installHref = isHome ? '#install' : '/#install';

  return (
    <>
      <a className="skip-link" href="#main-content">
        Skip to content
      </a>
      <header className={`site-nav${isScrolled ? ' is-scrolled' : ''}`}>
        <div className="wrap nav-bar">
          <a href="/" className="wordmark" aria-label="CodeExplorer home">
            <svg className="wordmark-glyph" viewBox="0 0 32 32" aria-hidden="true">
              <circle
                cx="16"
                cy="16"
                r="13"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeDasharray="2 3"
              />
              <circle
                cx="16"
                cy="16"
                r="7.5"
                fill="none"
                stroke="currentColor"
                strokeWidth="1.5"
                opacity=".55"
              />
              <circle cx="16" cy="16" r="3" fill="var(--signal)" />
              <circle cx="27.2" cy="9.5" r="2" fill="var(--svc)" />
              <circle cx="6" cy="22" r="2" fill="var(--db)" />
            </svg>
            <span>CodeExplorer</span>
          </a>

          <nav aria-label="Primary">
            <ul className="nav-links">
              <li className="nav-anchor-item">
                <a href={readersHref}>Product</a>
              </li>
              <li className="nav-anchor-item">
                <a href={queryHref}>Query</a>
              </li>
              <li className="nav-anchor-item">
                <a href={gatesHref}>Gates</a>
              </li>
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
              <li>
                <a
                  href="https://github.com/vmikhailov/code-explorer"
                  target="_blank"
                  rel="noopener"
                >
                  GitHub
                </a>
              </li>
            </ul>
          </nav>

          <div className="nav-actions">
            <a
              href="https://github.com/vmikhailov/code-explorer/releases/latest"
              target="_blank"
              rel="noopener"
              className="nav-version"
              id="heroReleaseBadge"
              aria-label="Latest CodeExplorer release"
            >
              {releaseVersion}
            </a>
            <a href={installHref} className="btn btn--primary btn--sm">
              Install
            </a>
          </div>
        </div>
      </header>
    </>
  );
};
