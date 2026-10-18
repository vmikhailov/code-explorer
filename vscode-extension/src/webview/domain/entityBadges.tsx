import React from 'react';
import { EntityKind } from './types';

export interface EntityBadgeMeta {
  kind: EntityKind | string;
  code: string;
  label: string;
  bgColor: string;
  borderColor: string;
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
  Topic: {
    kind: 'Topic',
    code: 'MSG',
    label: 'Topic',
    bgColor: '#f59e0b',
    borderColor: '#b45309',
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
    rawIconSvg: `
      <path d="M23 29.5 h18 a5.5 5.5 0 0 0 2 -10.5 a7 7 0 0 0 -13 -3 a5.5 5.5 0 0 0 -7 13.5 z" />
    `,
    iconPaths: <path d="M23 29.5 h18 a5.5 5.5 0 0 0 2 -10.5 a7 7 0 0 0 -13 -3 a5.5 5.5 0 0 0 -7 13.5 z" />,
  },
  Library: {
    kind: 'Library',
    code: 'LIB',
    label: 'Library',
    bgColor: '#4b5563',
    borderColor: '#374151',
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
};

export function getEntityBadgeMeta(kind: EntityKind | string): EntityBadgeMeta {
  switch (kind) {
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
    case 'Topic':
    case 'topic':
    case 'Topics':
    case 'MSG':
      return ENTITY_BADGES.Topic;
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

/**
 * Returns a Data URI for the circular graph node badge containing the white vector icon
 * and 3-letter uppercase code. Cached for fast Cytoscape rendering.
 */
export function getNodeBadgeSvgDataUri(kind: EntityKind | string): string {
  const cached = BADGE_SVG_CACHE.get(kind);
  if (cached) return cached;

  const meta = getEntityBadgeMeta(kind);
  const rawSvg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64"><g stroke="#ffffff" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" fill="none" opacity="0.95">${meta.rawIconSvg.trim()}</g><text x="32" y="50" text-anchor="middle" fill="#ffffff" font-family="system-ui, -apple-system, Segoe UI, Roboto, sans-serif" font-size="11.5" font-weight="800" letter-spacing="0.8">${meta.code}</text></svg>`;

  const uri = `data:image/svg+xml;utf8,${encodeURIComponent(rawSvg.replace(/\s+/g, ' '))}`;
  BADGE_SVG_CACHE.set(kind, uri);
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
 * Renders the circular node badge disc matching the graph node ontology:
 * - When `showCode === true` (or large badges), displays the icon + 3-letter code.
 * - When `showCode === false` (default for compact sizes), displays the crisp centered vector icon.
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

  return (
    <span
      className={className}
      title={tooltip}
      style={{
        width: size,
        height: size,
        minWidth: size,
        borderRadius: '50%',
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
