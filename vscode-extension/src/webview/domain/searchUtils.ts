/**
 * Evaluates whether an entity matches all space-separated search query tokens independently.
 * E.g., query "bq calc" matches "bq-routes-calculation".
 */
export function matchesSearchTokens(
  targetText: string | (string | undefined | null)[],
  query: string
): boolean {
  if (!query || !query.trim()) {
    return true;
  }

  const tokens = query.toLowerCase().trim().split(/\s+/).filter(Boolean);
  if (tokens.length === 0) {
    return true;
  }

  const combined = (
    Array.isArray(targetText)
      ? targetText.filter(Boolean).join(' ')
      : (targetText || '')
  ).toLowerCase();

  return tokens.every((token) => combined.includes(token));
}

/**
 * Converts a wildcard string (e.g. `user-*`, `*.Tests`, `order?`) into an anchored RegExp string.
 */
function wildcardToRegExpString(pattern: string): string {
  // Escape regex special characters except * and ?
  const escaped = pattern.replace(/[.+^$[\](){}|\\/]/g, '\\$&');
  // Replace wildcard characters with regex equivalents
  const regexStr = escaped.replace(/\*/g, '.*').replace(/\?/g, '.');
  return `^${regexStr}$`;
}

/**
 * Evaluates whether targetText matches query using wildcard (*, ?), RegExp, or token/substring search.
 *
 * Supported query styles:
 * 1. Explicit RegExp literal: `/pattern/flags` (e.g. `/^user-\d+/i` or `/service$/`)
 * 2. Wildcard patterns: `user-*`, `*Service`, `*.Tests`, `order?`
 * 3. Implicit RegExp patterns: `^user-`, `(auth|billing)`, `order\d+`
 * 4. Tokenized space-separated strings: `user service` (via matchesSearchTokens fallback)
 */
export function matchesSearchPattern(
  targetText: string | (string | undefined | null)[],
  query: string
): boolean {
  if (!query || !query.trim()) {
    return true;
  }

  const q = query.trim();
  const candidates: string[] = Array.isArray(targetText)
    ? targetText.filter((t): t is string => Boolean(t))
    : [targetText || ''];
  const combined = candidates.join(' ');

  // 1. Explicit RegExp literal: /pattern/flags
  const regexLiteralMatch = q.match(/^\/(.+)\/([a-z]*)$/i);
  if (regexLiteralMatch) {
    try {
      const pattern = regexLiteralMatch[1];
      const flags = regexLiteralMatch[2] || 'i';
      const re = new RegExp(pattern, flags);
      return candidates.some((t) => re.test(t)) || re.test(combined);
    } catch {
      // Incomplete/invalid regex while typing; fall through
    }
  }

  // 2. Wildcard pattern containing '*' or '?'
  if (q.includes('*') || q.includes('?')) {
    try {
      const re = new RegExp(wildcardToRegExpString(q), 'i');
      return candidates.some((t) => re.test(t)) || re.test(combined);
    } catch {
      // Fall through
    }
  }

  // 3. Implicit RegExp patterns containing regex syntax characters
  if (/[\^$|()[\]{}\\]/.test(q)) {
    try {
      const re = new RegExp(q, 'i');
      return candidates.some((t) => re.test(t)) || re.test(combined);
    } catch {
      // Incomplete/invalid regex while typing; fall through to token match
    }
  }

  // 4. Default: tokenized space-separated substring matching
  return matchesSearchTokens(targetText, query);
}
