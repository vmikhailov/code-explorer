import { EngineVersionConfig, ParsedSemver } from './types';

/**
 * Parses a version string into a structured SemVer representation.
 * Handles prefixes ("v", "ce ", "ce-"), build metadata ("+..."),
 * prereleases ("-..."), and 4-part .NET assembly versions.
 */
export function parseSemver(version: string): ParsedSemver | null {
  if (!version || typeof version !== 'string') return null;

  const trimmed = version.trim();
  // Strip leading non-digits (e.g. "v1.22.4", "ce 1.22.4", "ce-1.22.4")
  const cleaned = trimmed.replace(/^[^0-9]*/, '');
  if (!cleaned) return null;

  let buildMetadata: string | undefined;
  let withoutBuild = cleaned;
  const plusIdx = cleaned.indexOf('+');
  if (plusIdx !== -1) {
    buildMetadata = cleaned.slice(plusIdx + 1);
    withoutBuild = cleaned.slice(0, plusIdx);
  }

  let prerelease: string | undefined;
  let core = withoutBuild;
  const dashIdx = withoutBuild.indexOf('-');
  if (dashIdx !== -1) {
    prerelease = withoutBuild.slice(dashIdx + 1);
    core = withoutBuild.slice(0, dashIdx);
  }

  const parts = core.split('.').map((p) => parseInt(p, 10));
  if (isNaN(parts[0])) return null;

  const major = parts[0];
  const minor = parts.length > 1 && !isNaN(parts[1]) ? parts[1] : 0;
  const patch = parts.length > 2 && !isNaN(parts[2]) ? parts[2] : 0;
  const buildNumber = parts.length > 3 && !isNaN(parts[3]) ? parts[3] : undefined;

  return {
    major,
    minor,
    patch,
    buildNumber,
    prerelease,
    buildMetadata,
    raw: trimmed,
  };
}

/**
 * Normalizes a version string to clean canonical semver "MAJOR.MINOR.PATCH[-PRERELEASE]".
 * Strips build metadata (+...) and normalizes 4-part assembly versions.
 */
export function cleanSemver(version: string): string {
  const p = parseSemver(version);
  if (!p) return version.replace(/^v/, '').trim();
  const pre = p.prerelease ? `-${p.prerelease}` : '';
  return `${p.major}.${p.minor}.${p.patch}${pre}`;
}

/**
 * Returns true if two versions differ ONLY in build metadata (+...),
 * 4th component build/revision numbers (.x), or prerelease tags,
 * while having identical core major, minor, and patch numbers.
 */
export function isBuildDifferenceOnly(a: string, b: string): boolean {
  const pa = parseSemver(a);
  const pb = parseSemver(b);
  if (!pa || !pb) return false;

  return pa.major === pb.major && pa.minor === pb.minor && pa.patch === pb.patch;
}

/**
 * Compares two semver strings according to SemVer 2.0.0 specification.
 * Build metadata (+...) is completely ignored per SemVer Section 10.
 * Returns > 0 if a > b, < 0 if a < b, 0 if equal.
 */
export function compareSemver(a: string, b: string): number {
  const pa = parseSemver(a);
  const pb = parseSemver(b);

  if (!pa && !pb) return 0;
  if (!pa) return -1;
  if (!pb) return 1;

  if (pa.major !== pb.major) return pa.major - pb.major;
  if (pa.minor !== pb.minor) return pa.minor - pb.minor;
  if (pa.patch !== pb.patch) return pa.patch - pb.patch;

  // If 4th component (build number) is present on either, compare it
  const bna = pa.buildNumber ?? 0;
  const bnb = pb.buildNumber ?? 0;
  if (bna !== bnb) return bna - bnb;

  // Prerelease comparison:
  // If neither has prerelease, they are equal (build metadata is ignored!)
  if (!pa.prerelease && !pb.prerelease) return 0;
  // A version with prerelease has lower precedence than a normal version (Section 9)
  if (!pa.prerelease && pb.prerelease) return 1;
  if (pa.prerelease && !pb.prerelease) return -1;

  // Both have prereleases: compare dot-separated identifiers
  const preA = pa.prerelease!.split('.');
  const preB = pb.prerelease!.split('.');
  for (let i = 0; i < Math.max(preA.length, preB.length); i++) {
    const p1 = preA[i];
    const p2 = preB[i];
    if (p1 === undefined) return -1;
    if (p2 === undefined) return 1;
    if (p1 === p2) continue;

    const num1 = parseInt(p1, 10);
    const num2 = parseInt(p2, 10);
    const isNum1 = !isNaN(num1) && String(num1) === p1;
    const isNum2 = !isNaN(num2) && String(num2) === p2;

    if (isNum1 && isNum2) {
      if (num1 !== num2) return num1 - num2;
    } else if (isNum1) {
      return -1;
    } else if (isNum2) {
      return 1;
    } else {
      return p1.localeCompare(p2);
    }
  }

  return 0;
}

/**
 * Checks if a version satisfies a semver pattern or requirement.
 * Tolerant to build differences (+build metadata, 4th segment build numbers, prerelease tags).
 * Respects SemVer 2.0.0 rules for caret (^), tilde (~), wildcards (*), and comparison ranges.
 */
export function matchesEnginePattern(version: string, pattern: string): boolean {
  if (!pattern || pattern === '*' || pattern === 'latest') {
    return true;
  }

  const vParsed = parseSemver(version);
  if (!vParsed) return false;

  const cleanPattern = pattern.trim().replace(/^v/, '');

  // 1. Wildcard patterns: e.g. "1.22.*", "1.22.x", "1.*", "1.x"
  if (cleanPattern.endsWith('.*') || cleanPattern.endsWith('.x')) {
    const prefix = cleanPattern.slice(0, -2);
    const prefixParts = prefix.split('.').map((p) => parseInt(p, 10));
    if (prefixParts.length === 1 && !isNaN(prefixParts[0])) {
      return vParsed.major === prefixParts[0];
    }
    if (prefixParts.length >= 2 && !isNaN(prefixParts[0]) && !isNaN(prefixParts[1])) {
      return vParsed.major === prefixParts[0] && vParsed.minor === prefixParts[1];
    }
  }

  // 2. Caret pattern: e.g. "^1.22.0"
  // Per SemVer: allows changes that do not modify the leftmost non-zero element.
  // For major >= 1: >= BASE < (MAJOR + 1).0.0
  if (cleanPattern.startsWith('^')) {
    const baseStr = cleanPattern.slice(1);
    const baseParsed = parseSemver(baseStr);
    if (!baseParsed) return false;

    // Major version mismatch is a breaking change
    if (vParsed.major !== baseParsed.major) {
      return false;
    }

    if (baseParsed.major > 0) {
      // Same major version (>= 1):
      // A higher minor version is backwards-compatible in SemVer
      if (vParsed.minor > baseParsed.minor) {
        return true;
      }
      if (vParsed.minor === baseParsed.minor) {
        if (vParsed.patch >= baseParsed.patch) {
          return true;
        }
        return isBuildDifferenceOnly(version, baseStr);
      }
      return false;
    } else {
      // Major is 0: ^0.X.Y
      if (baseParsed.minor > 0) {
        return (
          vParsed.minor === baseParsed.minor &&
          (vParsed.patch >= baseParsed.patch || isBuildDifferenceOnly(version, baseStr))
        );
      } else {
        return vParsed.minor === 0 && vParsed.patch === baseParsed.patch;
      }
    }
  }

  // 3. Tilde pattern: e.g. "~1.22.0"
  // Per SemVer: allows patch-level changes if minor is specified: >= BASE < (MAJOR).(MINOR+1).0
  if (cleanPattern.startsWith('~')) {
    const baseStr = cleanPattern.slice(1);
    const baseParsed = parseSemver(baseStr);
    if (!baseParsed) return false;

    if (vParsed.major !== baseParsed.major || vParsed.minor !== baseParsed.minor) {
      return false;
    }
    return vParsed.patch >= baseParsed.patch || isBuildDifferenceOnly(version, baseStr);
  }

  // 4. Comparison operator ranges: e.g. ">=1.20.0", "<2.0.0"
  if (cleanPattern.startsWith('>=')) {
    return compareSemver(version, cleanPattern.slice(2)) >= 0;
  }
  if (cleanPattern.startsWith('>')) {
    return compareSemver(version, cleanPattern.slice(1)) > 0;
  }
  if (cleanPattern.startsWith('<=')) {
    return compareSemver(version, cleanPattern.slice(2)) <= 0;
  }
  if (cleanPattern.startsWith('<')) {
    return compareSemver(version, cleanPattern.slice(1)) < 0;
  }

  // 5. Exact version pattern: e.g. "1.22.4" or "=1.22.4"
  const targetExact = cleanPattern.startsWith('=') ? cleanPattern.slice(1) : cleanPattern;
  const targetParsed = parseSemver(targetExact);
  if (!targetParsed) return false;

  // Exact semver comparison ignores build metadata per SemVer Section 10
  if (compareSemver(version, targetExact) === 0) {
    return true;
  }

  // Tolerant to build differences (e.g. 1.22.4 vs 1.22.4.0 or 1.22.4+hash or 1.22.4-dev)
  return isBuildDifferenceOnly(version, targetExact);
}

/**
 * Derives the engine probing pattern and fallback version from the extension version.
 * If user explicitly set codeExplorer.engineVersion, that takes precedence.
 * Otherwise, dynamically generates `^${major}.${minor}.0` with fallback to clean `extensionVersion`.
 */
export function getEngineConfigFromExtensionVersion(
  extensionVersion: string,
  userSetting?: string
): EngineVersionConfig {
  const cleanExtVersion = cleanSemver(extensionVersion);
  const parsed = parseSemver(cleanExtVersion);
  const major = parsed ? parsed.major : 1;
  const minor = parsed ? parsed.minor : 0;

  if (userSetting && userSetting.trim().length > 0) {
    return {
      pattern: userSetting.trim(),
      fallback: cleanExtVersion,
    };
  }

  return {
    pattern: `^${major}.${minor}.0`,
    fallback: cleanExtVersion,
  };
}
