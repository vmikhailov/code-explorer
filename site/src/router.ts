import { ROUTES_DATA } from './generated/routesData';

const DEFAULT_ROUTE = '/';
const INDEX_HTML_ROUTE = '/index.html';
const SLASH_CHAR = '/';
const HTML_SUFFIX = '.html';
const EMPTY_STR = '';

export function normalizeRoute(pathname: string): string {
  const cleaned = pathname.trim();
  if (!cleaned || cleaned === SLASH_CHAR || cleaned === INDEX_HTML_ROUTE) {
    return DEFAULT_ROUTE;
  }
  const withLeadingSlash = cleaned.startsWith(SLASH_CHAR)
    ? cleaned
    : `${SLASH_CHAR}${cleaned}`;
  const withoutTrailingSlash =
    withLeadingSlash.length > 1 && withLeadingSlash.endsWith(SLASH_CHAR)
      ? withLeadingSlash.slice(0, -1)
      : withLeadingSlash;

  const withoutHtml = withoutTrailingSlash.endsWith(HTML_SUFFIX)
    ? withoutTrailingSlash.slice(0, -HTML_SUFFIX.length)
    : withoutTrailingSlash;

  if (ROUTES_DATA[withoutHtml]) {
    return withoutHtml;
  }

  return DEFAULT_ROUTE;
}

export function extractRouteFromHref(
  rawHref: string,
  currentOrigin: string,
  currentPathname: string,
): { route: string; hash: string; isSpaRoute: boolean } {
  const baseHref = `${currentOrigin}${currentPathname}`;
  const resolved = new URL(rawHref, baseHref);
  if (resolved.origin !== currentOrigin) {
    return { route: EMPTY_STR, hash: EMPTY_STR, isSpaRoute: false };
  }
  const rawPath = resolved.pathname;
  const stripped =
    rawPath.length > 1 && rawPath.endsWith(SLASH_CHAR)
      ? rawPath.slice(0, -1)
      : rawPath;
  const candidate =
    stripped === INDEX_HTML_ROUTE
      ? DEFAULT_ROUTE
      : stripped.endsWith(HTML_SUFFIX)
        ? stripped.slice(0, -HTML_SUFFIX.length)
        : stripped;

  const isKnown = Boolean(ROUTES_DATA[candidate]);
  return {
    route: isKnown ? candidate : DEFAULT_ROUTE,
    hash: resolved.hash,
    isSpaRoute: isKnown,
  };
}
