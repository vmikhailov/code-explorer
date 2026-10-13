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
