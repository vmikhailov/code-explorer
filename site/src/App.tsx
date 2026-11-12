import React, { useEffect, useState } from 'react';
import { SiteLayout } from './components/SiteLayout';
import { HomePage, HomeOverlays } from './pages/HomePage';
import { ArchitecturesPage } from './pages/ArchitecturesPage';
import { OntologyPage } from './pages/OntologyPage';
import { BenchmarksPage } from './pages/BenchmarksPage';
import { BenchmarkCasePage } from './pages/BenchmarkCasePage';
import { PrivacyPage } from './pages/PrivacyPage';
import { ROUTES_DATA } from './generated/routesData';
import { extractRouteFromHref, normalizeRoute } from './router';

export interface AppProps {
  initialPath: string;
}

const HOME_ROUTE = '/';
const ARCH_ROUTE = '/architectures';
const ONTOLOGY_ROUTE = '/ontology';
const BENCHMARKS_ROUTE = '/benchmarks';
const PRIVACY_ROUTE = '/privacy';

const POPSTATE_EVENT = 'popstate';
const CLICK_EVENT = 'click';
const ANCHOR_SELECTOR = 'a[href]';
const HREF_ATTR = 'href';
const TARGET_ATTR = 'target';
const DOWNLOAD_ATTR = 'download';
const DRAWER_ATTR = 'data-open-drawer';
const BLANK_TARGET = '_blank';
const SMOOTH_BEHAVIOR: ScrollBehavior = 'smooth';
const HASH_PREFIX = '#';
const TOP_COORDINATE = 0;

function scrollToHashOrTop(hash: string): void {
  if (hash && hash.startsWith(HASH_PREFIX) && hash.length > 1) {
    const id = hash.slice(1);
    const targetEl = document.getElementById(id);
    if (targetEl) {
      targetEl.scrollIntoView({ behavior: SMOOTH_BEHAVIOR });
      return;
    }
  }
  window.scrollTo(TOP_COORDINATE, TOP_COORDINATE);
}

export const App: React.FC<AppProps> = ({ initialPath }) => {
  const [currentRoute, setCurrentRoute] = useState<string>(() =>
    normalizeRoute(initialPath),
  );

  useEffect(() => {
    const entry = ROUTES_DATA[currentRoute];
    if (entry && typeof document !== 'undefined') {
      document.title = entry.title;
    }
  }, [currentRoute]);

  useEffect(() => {
    const onPopState = (): void => {
      const nextRoute = normalizeRoute(window.location.pathname);
      setCurrentRoute(nextRoute);
      const currentHash = window.location.hash;
      requestAnimationFrame(() => scrollToHashOrTop(currentHash));
    };

    const onDocumentClick = (event: MouseEvent): void => {
      if (
        event.defaultPrevented ||
        event.button !== 0 ||
        event.metaKey ||
        event.ctrlKey ||
        event.shiftKey ||
        event.altKey
      ) {
        return;
      }

      const targetNode = event.target as Element | null;
      if (!targetNode || typeof targetNode.closest !== 'function') {
        return;
      }

      const anchor = targetNode.closest(ANCHOR_SELECTOR) as HTMLAnchorElement | null;
      if (!anchor) {
        return;
      }

      if (
        anchor.getAttribute(TARGET_ATTR) === BLANK_TARGET ||
        anchor.hasAttribute(DOWNLOAD_ATTR) ||
        anchor.hasAttribute(DRAWER_ATTR)
      ) {
        return;
      }

      const rawHref = anchor.getAttribute(HREF_ATTR);
      if (!rawHref) {
        return;
      }

      if (rawHref.startsWith(HASH_PREFIX)) {
        return;
      }

      const parsed = extractRouteFromHref(
        rawHref,
        window.location.origin,
        window.location.pathname,
      );
      if (!parsed.isSpaRoute) {
        return;
      }

      event.preventDefault();
      const nextUrl = `${parsed.route}${parsed.hash}`;
      window.history.pushState(null, '', nextUrl);
      setCurrentRoute(parsed.route);
      requestAnimationFrame(() => scrollToHashOrTop(parsed.hash));
    };

    window.addEventListener(POPSTATE_EVENT, onPopState);
    document.addEventListener(CLICK_EVENT, onDocumentClick);
    return () => {
      window.removeEventListener(POPSTATE_EVENT, onPopState);
      document.removeEventListener(CLICK_EVENT, onDocumentClick);
    };
  }, []);

  let pageView: React.ReactNode;
  let afterFooter: React.ReactNode = null;

  if (currentRoute === HOME_ROUTE) {
    pageView = <HomePage />;
    afterFooter = <HomeOverlays />;
  } else if (currentRoute === ARCH_ROUTE) {
    pageView = <ArchitecturesPage />;
  } else if (currentRoute === ONTOLOGY_ROUTE) {
    pageView = <OntologyPage />;
  } else if (currentRoute === BENCHMARKS_ROUTE) {
    pageView = <BenchmarksPage />;
  } else if (currentRoute === PRIVACY_ROUTE) {
    pageView = <PrivacyPage />;
  } else {
    pageView = <BenchmarkCasePage routePath={currentRoute} />;
  }

  return (
    <SiteLayout currentRoute={currentRoute} afterFooter={afterFooter}>
      {pageView}
    </SiteLayout>
  );
};
