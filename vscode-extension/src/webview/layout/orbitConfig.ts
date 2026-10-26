import { ORBIT_TITLES } from './concentricLayout';

/**
 * Workspace-level description of how concentric orbits are arranged.
 *
 * `orbits` lists orbits from the center outward. Each entry holds the echelon tiers that share
 * that orbit, so a merged orbit is simply an entry with more than one tier and adjacency is
 * guaranteed by construction. `kindTiers` moves a whole class of entities (for example every
 * `Topic`) onto the orbit that contains the given tier.
 */
export interface OrbitConfig {
  version: 1;
  orbits: number[][];
  kindTiers: Record<string, number>;
}

export const ALL_ORBIT_TIERS = [0, 1, 2, 3, 4, 5] as const;

/** App, First echelon, Messaging, Second echelon, then Databases + External as one orbit. */
export const DEFAULT_ORBIT_CONFIG: OrbitConfig = {
  version: 1,
  orbits: [[0], [1], [2], [3], [4, 5]],
  kindTiers: {},
};

/** Project Map (C3): Orbit 0 EntryPoints/Endpoints, Orbits 1..4 Classes by BFS depth, Orbit 5 Transports/Buses/Libraries, Orbit 6 Databases/Tables. */
export const DEFAULT_PROJECT_MAP_ORBIT_CONFIG: OrbitConfig = {
  version: 1,
  orbits: [[0], [1], [2], [3], [4], [5], [6]],
  kindTiers: {},
};

const isTier = (v: unknown): v is number =>
  typeof v === 'number' && Number.isInteger(v) && v >= 0 && v <= 6;

export function cloneOrbitConfig(cfg: OrbitConfig): OrbitConfig {
  return {
    version: 1,
    orbits: cfg.orbits.map((o) => [...o]),
    kindTiers: { ...cfg.kindTiers },
  };
}

/** Accepts anything read from disk and returns a config that is always safe to use. */
export function sanitizeOrbitConfig(raw: unknown): OrbitConfig {
  const src = (raw && typeof raw === 'object' ? raw : {}) as Partial<OrbitConfig>;
  const seen = new Set<number>();
  const orbits: number[][] = [];

  if (Array.isArray(src.orbits)) {
    for (const entry of src.orbits) {
      if (!Array.isArray(entry)) continue;
      const tiers: number[] = [];
      for (const t of entry) {
        if (isTier(t) && !seen.has(t)) {
          seen.add(t);
          tiers.push(t);
        }
      }
      if (tiers.length > 0) orbits.push(tiers);
    }
  }

  if (orbits.length === 0) {
    return cloneOrbitConfig(DEFAULT_ORBIT_CONFIG);
  }

  for (const t of ALL_ORBIT_TIERS) {
    if (!seen.has(t)) orbits.push([t]);
  }

  const kindTiers: Record<string, number> = {};
  if (src.kindTiers && typeof src.kindTiers === 'object') {
    for (const [kind, tier] of Object.entries(src.kindTiers)) {
      if (isTier(tier)) kindTiers[kind] = tier;
    }
  }

  return { version: 1, orbits, kindTiers };
}

export function isDefaultOrbitConfig(cfg: OrbitConfig): boolean {
  return JSON.stringify(cfg) === JSON.stringify(DEFAULT_ORBIT_CONFIG);
}

/** The tier that identifies the orbit containing `tier` (the first tier of its entry). */
export function orbitRepresentative(cfg: OrbitConfig, tier: number): number {
  const entry = cfg.orbits.find((o) => o.includes(tier));
  return entry ? entry[0] : tier;
}

/** Orbit a node belongs to: a kind override wins over the computed echelon tier. */
export function effectiveOrbitTier(cfg: OrbitConfig, kind: string | undefined, baseTier: number): number {
  const override = kind ? cfg.kindTiers[kind] : undefined;
  return orbitRepresentative(cfg, override ?? baseTier);
}

/** Orbit representatives from the center outward, in the shape layouts accept as a custom order. */
export function orbitOrder(cfg: OrbitConfig): number[] {
  return cfg.orbits.map((o) => o[0]);
}

export function orbitTitle(tiers: number[], customTitles?: Record<number, string>): string {
  const titles: string[] = [];
  for (const t of tiers) {
    const title = customTitles?.[t] || ORBIT_TITLES[t] || `Tier ${t}`;
    if (!titles.includes(title)) titles.push(title);
  }
  return titles.join(' + ');
}

/** Merges the contiguous orbits `fromIndex..toIndex` (inclusive) into a single orbit. */
export function mergeOrbits(cfg: OrbitConfig, fromIndex: number, toIndex: number): OrbitConfig {
  const lo = Math.max(0, Math.min(fromIndex, toIndex));
  const hi = Math.min(cfg.orbits.length - 1, Math.max(fromIndex, toIndex));
  if (hi <= lo) return cfg;
  const next = cloneOrbitConfig(cfg);
  const merged = next.orbits.slice(lo, hi + 1).flat();
  next.orbits.splice(lo, hi - lo + 1, merged);
  return next;
}

export function splitOrbit(cfg: OrbitConfig, index: number): OrbitConfig {
  const entry = cfg.orbits[index];
  if (!entry || entry.length < 2) return cfg;
  const next = cloneOrbitConfig(cfg);
  next.orbits.splice(index, 1, ...entry.map((t) => [t]));
  return next;
}

export function moveOrbit(cfg: OrbitConfig, fromIndex: number, toIndex: number): OrbitConfig {
  if (
    fromIndex === toIndex ||
    fromIndex < 0 ||
    toIndex < 0 ||
    fromIndex >= cfg.orbits.length ||
    toIndex >= cfg.orbits.length
  ) {
    return cfg;
  }
  const next = cloneOrbitConfig(cfg);
  const [moved] = next.orbits.splice(fromIndex, 1);
  next.orbits.splice(toIndex, 0, moved);
  return next;
}

/** Sends every entity of `kind` to the orbit containing `tier`; `null` removes the override. */
export function moveKindToOrbit(cfg: OrbitConfig, kind: string, tier: number | null): OrbitConfig {
  const next = cloneOrbitConfig(cfg);
  if (tier === null) {
    delete next.kindTiers[kind];
  } else {
    next.kindTiers[kind] = tier;
  }
  return next;
}
