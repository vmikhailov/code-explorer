import React from 'react';
import { EntityKind, NodeContentMode } from './types';

export type NodeShape = 'ellipse' | 'round-rectangle';

export interface EntityBadgeMeta {
  kind: EntityKind | string;
  code: string;
  label: string;
  bgColor: string;
  borderColor: string;
  shape: NodeShape;
  iconPaths: React.ReactNode;
  rawIconSvg: string;
}

export const ENTITY_BADGES: Record<string, EntityBadgeMeta> = {
  Service: {
    kind: 'Service',
    code: 'SVC',
    label: 'Service',
    bgColor: '#e53935',
    borderColor: '#7f1d1d',
    shape: 'ellipse',
    rawIconSvg: `
      <path d="M32 12 L43 18 L43 30 L32 36 L21 30 L21 18 Z" />
      <path d="M32 24 L32 36 M32 24 L21 18 M32 24 L43 18" />
    `,
    iconPaths: (
      <>
        <path d="M32 12 L43 18 L43 30 L32 36 L21 30 L21 18 Z" />
        <path d="M32 24 L32 36 M32 24 L21 18 M32 24 L43 18" />
      </>
    ),
  },
  App: {
    kind: 'App',
    code: 'APP',
    label: 'App',
    bgColor: '#0288d1',
    borderColor: '#01579b',
    shape: 'ellipse',
    rawIconSvg: `
      <rect x="19" y="14" width="26" height="19" rx="2.5" />
      <line x1="19" y1="20.5" x2="45" y2="20.5" />
      <circle cx="23.5" cy="17.2" r="0.9" fill="#ffffff" stroke="none" />
      <circle cx="26.5" cy="17.2" r="0.9" fill="#ffffff" stroke="none" />
      <circle cx="29.5" cy="17.2" r="0.9" fill="#ffffff" stroke="none" />
      <rect x="23" y="24" width="6" height="5.5" rx="1" stroke-width="1.8" />
      <line x1="32.5" y1="24.5" x2="40.5" y2="24.5" stroke-width="1.8" />
      <line x1="32.5" y1="28.5" x2="37.5" y2="28.5" stroke-width="1.8" />
    `,
    iconPaths: (
      <>
        <rect x="19" y="14" width="26" height="19" rx="2.5" />
        <line x1="19" y1="20.5" x2="45" y2="20.5" />
        <circle cx="23.5" cy="17.2" r="0.9" fill="#ffffff" stroke="none" />
        <circle cx="26.5" cy="17.2" r="0.9" fill="#ffffff" stroke="none" />
        <circle cx="29.5" cy="17.2" r="0.9" fill="#ffffff" stroke="none" />
        <rect x="23" y="24" width="6" height="5.5" rx="1" strokeWidth="1.8" />
        <line x1="32.5" y1="24.5" x2="40.5" y2="24.5" strokeWidth="1.8" />
        <line x1="32.5" y1="28.5" x2="37.5" y2="28.5" strokeWidth="1.8" />
      </>
    ),
  },
  Worker: {
    kind: 'Worker',
    code: 'WRK',
    label: 'Worker',
    bgColor: '#c026d3',
    borderColor: '#86198f',
    shape: 'ellipse',
    rawIconSvg: `
      <path d="M34 13 L23.5 24 L31.5 24 L29 35.5 L40.5 23 L32.5 23 Z" />
    `,
    iconPaths: <path d="M34 13 L23.5 24 L31.5 24 L29 35.5 L40.5 23 L32.5 23 Z" />,
  },
  CliTool: {
    kind: 'CliTool',
    code: 'CLI',
    label: 'CLI Tool',
    bgColor: '#d97706',
    borderColor: '#92400e',
    shape: 'ellipse',
    rawIconSvg: `
      <rect x="19" y="14" width="26" height="19" rx="3" />
      <path d="M24.5 20.5 L29 23.5 L24.5 26.5" />
      <line x1="32" y1="26.5" x2="38" y2="26.5" />
    `,
    iconPaths: (
      <>
        <rect x="19" y="14" width="26" height="19" rx="3" />
        <path d="M24.5 20.5 L29 23.5 L24.5 26.5" />
        <line x1="32" y1="26.5" x2="38" y2="26.5" />
      </>
    ),
  },
  Database: {
    kind: 'Database',
    code: 'DB',
    label: 'Database',
    bgColor: '#7b1fa2',
    borderColor: '#4a148c',
    shape: 'round-rectangle',
    rawIconSvg: `
      <ellipse cx="32" cy="16" rx="11" ry="3.8" />
      <path d="M21 16 v15 a11 3.8 0 0 0 22 0 v-15" />
      <path d="M21 23.5 a11 3.8 0 0 0 22 0" />
    `,
    iconPaths: (
      <>
        <ellipse cx="32" cy="16" rx="11" ry="3.8" />
        <path d="M21 16 v15 a11 3.8 0 0 0 22 0 v-15" />
        <path d="M21 23.5 a11 3.8 0 0 0 22 0" />
      </>
    ),
  },
  Table: {
    kind: 'Table',
    code: 'TBL',
    label: 'Table',
    bgColor: '#7b1fa2',
    borderColor: '#4a148c',
    shape: 'round-rectangle',
    rawIconSvg: `
      <rect x="19" y="14" width="26" height="20" rx="2.5" />
      <line x1="19" y1="20.5" x2="45" y2="20.5" />
      <line x1="19" y1="27" x2="45" y2="27" />
      <line x1="28" y1="20.5" x2="28" y2="34" />
    `,
    iconPaths: (
      <>
        <rect x="19" y="14" width="26" height="20" rx="2.5" />
        <line x1="19" y1="20.5" x2="45" y2="20.5" />
        <line x1="19" y1="27" x2="45" y2="27" />
        <line x1="28" y1="20.5" x2="28" y2="34" />
      </>
    ),
  },
  Procedure: {
    kind: 'Procedure',
    code: 'PRC',
    label: 'Stored Procedure',
    bgColor: '#6d28d9',
    borderColor: '#4c1d95',
    shape: 'round-rectangle',
    rawIconSvg: `
      <ellipse cx="32" cy="15.5" rx="10" ry="3.2" />
      <path d="M22 15.5 v14 a10 3.2 0 0 0 20 0 v-14" />
      <path d="M28 22.5 L31.5 25 L28 27.5 M33 27.5 H36.5" />
    `,
    iconPaths: (
      <>
        <ellipse cx="32" cy="15.5" rx="10" ry="3.2" />
        <path d="M22 15.5 v14 a10 3.2 0 0 0 20 0 v-14" />
        <path d="M28 22.5 L31.5 25 L28 27.5 M33 27.5 H36.5" />
      </>
    ),
  },
  Module: {
    kind: 'Module',
    code: 'MOD',
    label: 'Module',
    bgColor: '#2563eb',
    borderColor: '#1d4ed8',
    shape: 'ellipse',
    rawIconSvg: `
      <path d="M32 13 L43 19 L43 31 L32 37 L21 31 L21 19 Z" />
      <path d="M32 25 L32 37 M32 25 L21 19 M32 25 L43 19" />
    `,
    iconPaths: (
      <>
        <path d="M32 13 L43 19 L43 31 L32 37 L21 31 L21 19 Z" />
        <path d="M32 25 L32 37 M32 25 L21 19 M32 25 L43 19" />
      </>
    ),
  },
  Topic: {
    kind: 'Topic',
    code: 'MSG',
    label: 'Topic',
    bgColor: '#f59e0b',
    borderColor: '#b45309',
    shape: 'round-rectangle',
    rawIconSvg: `
      <rect x="19.5" y="16" width="25" height="17" rx="2.5" />
      <path d="M20.5 17.5 L32 25.5 L43.5 17.5" />
    `,
    iconPaths: (
      <>
        <rect x="19.5" y="16" width="25" height="17" rx="2.5" />
        <path d="M20.5 17.5 L32 25.5 L43.5 17.5" />
      </>
    ),
  },
  ExternalService: {
    kind: 'ExternalService',
    code: 'EXT',
    label: 'External Service',
    bgColor: '#26a69a',
    borderColor: '#004d40',
    shape: 'round-rectangle',
    rawIconSvg: `
      <path d="M23 29.5 h18 a5.5 5.5 0 0 0 2 -10.5 a7 7 0 0 0 -13 -3 a5.5 5.5 0 0 0 -7 13.5 z" />
    `,
    iconPaths: <path d="M23 29.5 h18 a5.5 5.5 0 0 0 2 -10.5 a7 7 0 0 0 -13 -3 a5.5 5.5 0 0 0 -7 13.5 z" />,
  },
  CloudService: {
    kind: 'CloudService',
    code: 'AWS',
    label: 'Cloud Resource',
    bgColor: '#0284c7',
    borderColor: '#075985',
    shape: 'round-rectangle',
    rawIconSvg: `
      <path d="M23 29.5 h18 a5.5 5.5 0 0 0 2 -10.5 a7 7 0 0 0 -13 -3 a5.5 5.5 0 0 0 -7 13.5 z" />
    `,
    iconPaths: <path d="M23 29.5 h18 a5.5 5.5 0 0 0 2 -10.5 a7 7 0 0 0 -13 -3 a5.5 5.5 0 0 0 -7 13.5 z" />,
  },
  PeerMesh: {
    kind: 'PeerMesh',
    code: 'MESH',
    label: 'Peer Mesh',
    bgColor: '#0f766e',
    borderColor: '#134e4a',
    shape: 'round-rectangle',
    rawIconSvg: `
      <circle cx="32" cy="16" r="4" />
      <circle cx="21" cy="31" r="4" />
      <circle cx="43" cy="31" r="4" />
      <circle cx="32" cy="40" r="4" />
      <path d="M29.7 19.3 L23.3 27.7 M34.3 19.3 L40.7 27.7 M25 31 L39 31 M23.5 34 L29.5 38" />
    `,
    iconPaths: (
      <>
        <circle cx="32" cy="16" r="4" />
        <circle cx="21" cy="31" r="4" />
        <circle cx="43" cy="31" r="4" />
        <circle cx="32" cy="40" r="4" />
        <path d="M29.7 19.3 L23.3 27.7 M34.3 19.3 L40.7 27.7 M25 31 L39 31 M23.5 34 L29.5 38" />
      </>
    ),
  },
  Library: {
    kind: 'Library',
    code: 'LIB',
    label: 'Library',
    bgColor: '#4b5563',
    borderColor: '#374151',
    shape: 'ellipse',
    rawIconSvg: `
      <path d="M32 14 L43 19 L32 24 L21 19 Z" />
      <path d="M21 24 L32 29 L43 24" />
      <path d="M21 29 L32 34 L43 29" />
    `,
    iconPaths: (
      <>
        <path d="M32 14 L43 19 L32 24 L21 19 Z" />
        <path d="M21 24 L32 29 L43 24" />
        <path d="M21 29 L32 34 L43 29" />
      </>
    ),
  },
  TestSuite: {
    kind: 'TestSuite',
    code: 'TST',
    label: 'Test Suite',
    bgColor: '#059669',
    borderColor: '#047857',
    shape: 'ellipse',
    rawIconSvg: `
      <path d="M32 13 L43 17 V24.5 C43 30.5 32 35.5 32 35.5 C32 35.5 21 30.5 21 24.5 V17 Z" />
      <path d="M27 24 L30.5 27.5 L37 21" stroke-width="2.6" />
    `,
    iconPaths: (
      <>
        <path d="M32 13 L43 17 V24.5 C43 30.5 32 35.5 32 35.5 C32 35.5 21 30.5 21 24.5 V17 Z" />
        <path d="M27 24 L30.5 27.5 L37 21" strokeWidth={2.6} />
      </>
    ),
  },
  Endpoint: {
    kind: 'Endpoint',
    code: 'EPT',
    label: 'Endpoint',
    bgColor: '#0288d1',
    borderColor: '#01579b',
    shape: 'ellipse',
    rawIconSvg: `
      <circle cx="24" cy="24" r="4" />
      <path d="M28 24 H42 M37 19 L42 24 L37 29" />
    `,
    iconPaths: (
      <>
        <circle cx="24" cy="24" r="4" />
        <path d="M28 24 H42 M37 19 L42 24 L37 29" />
      </>
    ),
  },
  EntryPoint: {
    kind: 'EntryPoint',
    code: 'ENT',
    label: 'EntryPoint',
    bgColor: '#0ea5e9',
    borderColor: '#0369a1',
    shape: 'ellipse',
    rawIconSvg: `
      <polygon points="25 16 41 25 25 34 25 16" />
    `,
    iconPaths: <polygon points="25 16 41 25 25 34 25 16" />,
  },
  Type: {
    kind: 'Type',
    code: 'CLS',
    label: 'Class',
    bgColor: '#e53935',
    borderColor: '#7f1d1d',
    shape: 'ellipse',
    rawIconSvg: `
      <rect x="20" y="14" width="24" height="20" rx="2.5" />
      <line x1="20" y1="21" x2="44" y2="21" />
      <line x1="24" y1="25.5" x2="36" y2="25.5" />
      <line x1="24" y1="29.5" x2="33" y2="29.5" />
    `,
    iconPaths: (
      <>
        <rect x="20" y="14" width="24" height="20" rx="2.5" />
        <line x1="20" y1="21" x2="44" y2="21" />
        <line x1="24" y1="25.5" x2="36" y2="25.5" />
        <line x1="24" y1="29.5" x2="33" y2="29.5" />
      </>
    ),
  },
  Group: {
    kind: 'Group',
    code: 'GRP',
    label: 'Group',
    bgColor: '#4f46e5',
    borderColor: '#312e81',
    shape: 'ellipse',
    rawIconSvg: `
      <polygon points="32 14 46 21 32 28 18 21 32 14" />
      <path d="M18 26 L32 33 L46 26" />
      <path d="M18 31 L32 38 L46 31" />
    `,
    iconPaths: (
      <>
        <polygon points="32 14 46 21 32 28 18 21 32 14" />
        <path d="M18 26 L32 33 L46 26" />
        <path d="M18 31 L32 38 L46 31" />
      </>
    ),
  },
};

export function getEntityBadgeMeta(kind: EntityKind | string): EntityBadgeMeta {
  switch (kind) {
    case 'Group':
    case 'group':
    case 'Groups':
    case 'GRP':
      return ENTITY_BADGES.Group;
    case 'App':
    case 'Ingress':
    case 'FrontendApp':
    case 'frontend':
      return ENTITY_BADGES.App;
    case 'Worker':
    case 'worker':
    case 'Workers':
      return ENTITY_BADGES.Worker;
    case 'CliTool':
    case 'cli':
    case 'CLI':
      return ENTITY_BADGES.CliTool;
    case 'Database':
    case 'database':
    case 'Databases':
    case 'DB':
      return ENTITY_BADGES.Database;
    case 'Table':
    case 'table':
    case 'Tables':
    case 'TBL':
      return ENTITY_BADGES.Table;
    case 'Procedure':
    case 'procedure':
    case 'PRC':
      return ENTITY_BADGES.Procedure;
    case 'Endpoint':
    case 'endpoint':
    case 'Endpoints':
    case 'EPT':
      return ENTITY_BADGES.Endpoint;
    case 'EntryPoint':
    case 'entrypoint':
    case 'EntryPoints':
    case 'ENT':
      return ENTITY_BADGES.EntryPoint;
    case 'Type':
    case 'Class':
    case 'class':
    case 'Classes':
    case 'CLS':
      return ENTITY_BADGES.Type;
    case 'Module':
    case 'ProjectModule':
    case 'module':
    case 'MOD':
      return ENTITY_BADGES.Module;
    case 'Topic':
    case 'topic':
    case 'Topics':
    case 'MSG':
      return ENTITY_BADGES.Topic;
    case 'CloudService':
    case 'cloudservice':
      return ENTITY_BADGES.CloudService;
    case 'PeerMesh':
    case 'peermesh':
      return ENTITY_BADGES.PeerMesh;
    case 'ExternalService':
    case 'external':
    case 'External':
      return ENTITY_BADGES.ExternalService;
    case 'Library':
    case 'SharedLibrary':
    case 'library':
    case 'Libraries':
      return ENTITY_BADGES.Library;
    case 'TestSuite':
    case 'test':
    case 'Test':
    case 'Tests':
    case 'test_suite':
      return ENTITY_BADGES.TestSuite;
    case 'Service':
    case 'service':
    case 'Services':
    default:
      return ENTITY_BADGES.Service;
  }
}

const BADGE_SVG_CACHE = new Map<string, string>();

export interface NodeBadgeOptions {
  isGroup?: boolean;
  count?: number;
  contentMode?: NodeContentMode;
  metricText?: string;
}

/**
 * Returns a Data URI for the graph node badge containing the white vector icon
 * and optional code or metric text. Cached for fast Cytoscape rendering.
 * Defaults to `contentMode: 'icon'` (clean centered vector icon without 3-letter acronym).
 */
export function getNodeBadgeSvgDataUri(
  kind: EntityKind | string,
  options?: NodeBadgeOptions
): string {
  const isGroup = Boolean(options?.isGroup);
  const count = options?.count;
  const contentMode: NodeContentMode = options?.contentMode ?? 'icon';
  const metricText = options?.metricText ?? '';
  const cacheKey = `${kind}:${contentMode}:${isGroup ? `grp:${count ?? ''}` : 'single'}:${metricText}`;
  const cached = BADGE_SVG_CACHE.get(cacheKey);
  if (cached) return cached;

  const meta = getEntityBadgeMeta(kind);
  const isRect = meta.shape === 'round-rectangle';
  const clusterRing = isGroup
    ? isRect
      ? '<rect x="4" y="4" width="56" height="56" rx="10" fill="none" stroke="rgba(255,255,255,0.45)" stroke-width="1.5" stroke-dasharray="4 2" />'
      : '<circle cx="32" cy="32" r="29" fill="none" stroke="rgba(255,255,255,0.45)" stroke-width="1.5" stroke-dasharray="4 2" />'
    : '';

  let bottomText = '';
  if (contentMode === 'icon-code') {
    bottomText = isGroup && count && count > 0 ? `${meta.code} ×${count}` : meta.code;
  } else if (contentMode === 'metric') {
    if (isGroup && count && count > 0) {
      bottomText = `×${count}`;
    } else {
      bottomText = metricText;
    }
  } else {
    // 'icon' mode: clean centered icon, only show ×N badge if it's a collapsed group
    if (isGroup && count && count > 0) {
      bottomText = `×${count}`;
    }
  }

  let rawSvg: string;
  if (!bottomText) {
    // Center and scale up the vector icon inside the 64x64 viewBox
    rawSvg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">${clusterRing}<g transform="translate(32, 32) scale(1.26) translate(-32, -24.5)" stroke="#ffffff" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" fill="none" opacity="0.96">${meta.rawIconSvg.trim()}</g></svg>`;
  } else {
    const fontSize = bottomText.length > 5 ? '9.5' : '11.5';
    const letterSpacing = bottomText.length > 5 ? '0.4' : '0.8';
    rawSvg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">${clusterRing}<g stroke="#ffffff" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" fill="none" opacity="0.95">${meta.rawIconSvg.trim()}</g><text x="32" y="50" text-anchor="middle" fill="#ffffff" font-family="system-ui, -apple-system, Segoe UI, Roboto, sans-serif" font-size="${fontSize}" font-weight="800" letter-spacing="${letterSpacing}">${bottomText}</text></svg>`;
  }

  const uri = `data:image/svg+xml;utf8,${encodeURIComponent(rawSvg.replace(/\s+/g, ' '))}`;
  BADGE_SVG_CACHE.set(cacheKey, uri);
  return uri;
}

export interface EntityNodeBadgeProps {
  kind: EntityKind | string;
  size?: number;
  showCode?: boolean;
  className?: string;
  style?: React.CSSProperties;
  title?: string;
}

/**
 * Renders the node badge matching the graph node ontology shape (circle for code/services,
 * rounded rectangle for data stores & external resources):
 * - When `showCode === true`, displays the icon + 3-letter code.
 * - When `showCode === false` (default), displays the crisp centered vector icon.
 */
export const EntityNodeBadge: React.FC<EntityNodeBadgeProps> = ({
  kind,
  size = 24,
  showCode = false,
  className,
  style,
  title,
}) => {
  const meta = getEntityBadgeMeta(kind);
  const borderWidth = size <= 18 ? 1 : size >= 36 ? 2 : 1.5;
  const tooltip = title ?? `${meta.label} (${meta.code})`;
  const borderRadius = meta.shape === 'round-rectangle' ? Math.max(4, Math.round(size * 0.22)) : '50%';

  return (
    <span
      className={className}
      title={tooltip}
      style={{
        width: size,
        height: size,
        minWidth: size,
        borderRadius,
        backgroundColor: meta.bgColor,
        border: `${borderWidth}px solid ${meta.borderColor}`,
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
        boxSizing: 'border-box',
        overflow: 'hidden',
        lineHeight: 1,
        userSelect: 'none',
        ...style,
      }}
    >
      {showCode ? (
        <svg
          viewBox="0 0 64 64"
          width="100%"
          height="100%"
          style={{ display: 'block' }}
        >
          <g
            stroke="#ffffff"
            strokeWidth="2.4"
            strokeLinecap="round"
            strokeLinejoin="round"
            fill="none"
            opacity="0.95"
          >
            {meta.iconPaths}
          </g>
          <text
            x="32"
            y="50"
            textAnchor="middle"
            fill="#ffffff"
            fontFamily="system-ui, -apple-system, Segoe UI, Roboto, sans-serif"
            fontSize="11.5"
            fontWeight="800"
            letterSpacing="0.8"
          >
            {meta.code}
          </text>
        </svg>
      ) : (
        <svg
          viewBox="14 6 36 36"
          width="68%"
          height="68%"
          style={{ display: 'block' }}
        >
          <g
            stroke="#ffffff"
            strokeWidth="2.4"
            strokeLinecap="round"
            strokeLinejoin="round"
            fill="none"
            opacity="0.95"
          >
            {meta.iconPaths}
          </g>
        </svg>
      )}
    </span>
  );
};

export interface EntityCodeBadgeProps {
  kind: EntityKind | string;
  className?: string;
  style?: React.CSSProperties;
}

/**
 * Compact pill badge showing the 3-letter code (`SVC`, `APP`, `DB`, etc.) in the entity's theme color.
 */
export const EntityCodeBadge: React.FC<EntityCodeBadgeProps> = ({ kind, className, style }) => {
  const meta = getEntityBadgeMeta(kind);
  return (
    <span
      className={className}
      style={{
        display: 'inline-block',
        fontSize: '9px',
        fontWeight: 700,
        padding: '1px 5px',
        borderRadius: 4,
        backgroundColor: meta.bgColor,
        color: '#ffffff',
        letterSpacing: '0.4px',
        lineHeight: 1.2,
        flexShrink: 0,
        userSelect: 'none',
        ...style,
      }}
    >
      {meta.code}
    </span>
  );
};
