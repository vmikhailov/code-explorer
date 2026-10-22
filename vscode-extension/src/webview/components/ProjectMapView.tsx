import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { GraphData } from '../../../../proto/types';
import { computeProjectMapLayout } from '../layout/projectMapLayout';
import {
  ProjectMapEdgeModel,
  ProjectMapNodeModel,
  RESOURCE_TIER,
  TIER_COLORS,
  filterProjectMap,
  nodeVisualRadius,
  parseProjectMapGraph,
  resourceBadge,
} from '../projectMap/projectMapModel';
import { ProjectMapInspector } from './ProjectMapInspector';

export interface ProjectMapViewProps {
  serverHttpUrl?: string;
  project?: string;
  onProjectChange?: (projectName: string) => void;
  onOpenFile: (filePath: string, lineStart?: number) => void;
}

type Selection = { type: 'node'; id: string } | { type: 'edge'; id: string } | null;

interface ViewTransform {
  scale: number;
  tx: number;
  ty: number;
}

function resolveBaseUrl(serverHttpUrl?: string): string | undefined {
  if (serverHttpUrl) return serverHttpUrl;
  const wsUrl = typeof window !== 'undefined' ? window.__CE_CONFIG__?.wsUrl : undefined;
  return wsUrl?.replace(/^ws:\/\//, 'http://').replace(/^wss:\/\//, 'https://').replace(/\/ws$/, '');
}

function edgePath(x1: number, y1: number, x2: number, y2: number, r1: number, r2: number, curved: boolean): string {
  const dx = x2 - x1;
  const dy = y2 - y1;
  const len = Math.hypot(dx, dy) || 1;
  const ux = dx / len;
  const uy = dy / len;
  const sx = x1 + ux * r1;
  const sy = y1 + uy * r1;
  const ex = x2 - ux * (r2 + 4);
  const ey = y2 - uy * (r2 + 4);
  if (!curved) return `M${sx},${sy} L${ex},${ey}`;
  // Bend perpendicular to the edge so A→B and B→A render as two separate arcs.
  const bend = Math.min(40, len * 0.15);
  const cx = (sx + ex) / 2 - uy * bend;
  const cy = (sy + ey) / 2 + ux * bend;
  return `M${sx},${sy} Q${cx},${cy} ${ex},${ey}`;
}

export const ProjectMapView: React.FC<ProjectMapViewProps> = ({ serverHttpUrl, project, onProjectChange, onOpenFile }) => {
  const [graph, setGraph] = useState<GraphData | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [selection, setSelection] = useState<Selection>(null);
  const [hovered, setHovered] = useState<string | null>(null);
  const [hideUtilities, setHideUtilities] = useState(false);
  const [hideResources, setHideResources] = useState(false);
  const [cyclesOnly, setCyclesOnly] = useState(false);
  const [transform, setTransform] = useState<ViewTransform>({ scale: 1, tx: 0, ty: 0 });
  const dragRef = useRef<{ x: number; y: number; tx: number; ty: number } | null>(null);
  const svgRef = useRef<SVGSVGElement | null>(null);

  useEffect(() => {
    const baseUrl = resolveBaseUrl(serverHttpUrl);
    if (!baseUrl) {
      setError('Server connection not ready yet.');
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    fetch(`${baseUrl}/api/view?view=project-map&scope=${encodeURIComponent(project ?? '')}`)
      .then(async (res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}: ${await res.text()}`);
        return (await res.json()) as GraphData;
      })
      .then((data) => {
        if (cancelled) return;
        setGraph(data);
        setSelection(null);
        setTransform({ scale: 1, tx: 0, ty: 0 });
      })
      .catch((err: unknown) => {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [serverHttpUrl, project, reloadToken]);

  const model = useMemo(() => parseProjectMapGraph(graph), [graph]);
  const visible = useMemo(
    () => filterProjectMap(model, { hideUtilities, hideResources, cyclesOnly }),
    [model, hideUtilities, hideResources, cyclesOnly]
  );

  const layout = useMemo(
    () =>
      computeProjectMapLayout(
        visible.nodes.map((n) => ({ id: n.id, tier: n.tier, radius: nodeVisualRadius(n) })),
        visible.edges.map((e) => ({ source: e.source, target: e.target }))
      ),
    [visible]
  );

  const nodeById = useMemo(() => new Map(visible.nodes.map((n) => [n.id, n])), [visible.nodes]);
  const reverseEdges = useMemo(() => new Set(visible.edges.map((e) => `${e.target}->${e.source}`)), [visible.edges]);
  const maxCount = useMemo(() => Math.max(1, ...visible.edges.map((e) => e.count)), [visible.edges]);

  const focusId = selection?.type === 'node' ? selection.id : hovered;
  const focusNeighbors = useMemo(() => {
    if (!focusId) return null;
    const set = new Set<string>([focusId]);
    for (const e of visible.edges) {
      if (e.source === focusId) set.add(e.target);
      if (e.target === focusId) set.add(e.source);
    }
    return set;
  }, [focusId, visible.edges]);

  const selectedNode = selection?.type === 'node' ? model.nodes.find((n) => n.id === selection.id) : undefined;
  const selectedEdge = selection?.type === 'edge' ? model.edges.find((e) => e.id === selection.id) : undefined;

  const handleWheel = useCallback((e: React.WheelEvent<SVGSVGElement>) => {
    const factor = Math.exp(-e.deltaY * 0.0015);
    setTransform((t) => ({ ...t, scale: Math.max(0.2, Math.min(4, t.scale * factor)) }));
  }, []);

  const handleMouseDown = (e: React.MouseEvent<SVGSVGElement>) => {
    if ((e.target as Element).closest('.pm-node, .pm-edge')) return;
    dragRef.current = { x: e.clientX, y: e.clientY, tx: transform.tx, ty: transform.ty };
  };
  const handleMouseMove = (e: React.MouseEvent<SVGSVGElement>) => {
    const d = dragRef.current;
    if (!d) return;
    const rect = svgRef.current?.getBoundingClientRect();
    const unitsPerPx = rect && rect.width > 0 ? (layout.extent * 2) / rect.width : 1;
    setTransform((t) => ({ ...t, tx: d.tx + (e.clientX - d.x) * unitsPerPx, ty: d.ty + (e.clientY - d.y) * unitsPerPx }));
  };
  const endDrag = () => {
    dragRef.current = null;
  };

  const ringLabel = (tier: number) =>
    model.tiers.find((t) => t.tier === tier)?.label ?? (tier === RESOURCE_TIER ? 'Resources' : `Tier ${tier}`);

  const moduleCount = model.nodes.filter((n) => !n.isResource).length;
  const resourceCount = model.nodes.length - moduleCount;
  const depCount = model.edges.filter((e) => !e.isResourceLink).length;
  const extent = Math.max(layout.extent, 200);

  return (
    <div className="pm-container">
      <div className="pm-hud">
        <div className="pm-hud-left">
          <span className="pm-title">Project Map</span>
          <select
            className="pm-select"
            value={model.projectName || project || ''}
            onChange={(e) => onProjectChange?.(e.target.value)}
            title="Select project"
          >
            {model.projects.length === 0 && <option value={project ?? ''}>{project || '(largest project)'}</option>}
            {model.projects.map((p) => (
              <option key={p.id} value={p.name} title={p.path}>
                {p.name} ({p.files} files)
              </option>
            ))}
          </select>
          <span className="pm-chip">{moduleCount} {model.granularity === 'file' ? 'files' : 'modules'}</span>
          <span className="pm-chip">{depCount} dependencies</span>
          {resourceCount > 0 && <span className="pm-chip">{resourceCount} resources</span>}
          {model.cycles.length > 0 && (
            <button
              className={`pm-chip pm-chip-warning ${cyclesOnly ? 'is-active' : ''}`}
              onClick={() => setCyclesOnly((v) => !v)}
              title="Show only modules involved in dependency cycles"
            >
              ⚠️ {model.cycles.length} module cycle{model.cycles.length === 1 ? '' : 's'}
            </button>
          )}
        </div>
        <div className="pm-hud-right">
          <label className="pm-toggle" title="Hide foundation/utility modules used by most of the project">
            <input type="checkbox" checked={!hideUtilities} onChange={() => setHideUtilities((v) => !v)} /> Utilities
          </label>
          <label className="pm-toggle" title="Show tables, topics and external services">
            <input type="checkbox" checked={!hideResources} onChange={() => setHideResources((v) => !v)} /> Resources
          </label>
          <button className="pm-icon-btn" onClick={() => setTransform({ scale: 1, tx: 0, ty: 0 })} title="Reset zoom">
            ⤢
          </button>
          <button className="pm-icon-btn" onClick={() => setReloadToken((t) => t + 1)} title="Reload">
            ⟳
          </button>
        </div>
      </div>

      {model.sourceRoot && (
        <div className="pm-subhud pm-muted">
          Source root: <code>{model.sourceRoot}</code>
          {model.testFilesExcluded > 0 && <> · {model.testFilesExcluded} test files excluded</>}
        </div>
      )}

      {loading && <div className="pm-overlay">Loading project map…</div>}
      {!loading && (error || model.error) && <div className="pm-overlay pm-error">{error || model.error}</div>}
      {!loading && !error && !model.error && graph && visible.nodes.length === 0 && (
        <div className="pm-overlay pm-muted">No modules match the current filters.</div>
      )}

      <svg
        ref={svgRef}
        className="pm-canvas"
        viewBox={`${-extent} ${-extent} ${extent * 2} ${extent * 2}`}
        onWheel={handleWheel}
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={endDrag}
        onMouseLeave={endDrag}
        onClick={(e) => {
          if (!(e.target as Element).closest('.pm-node, .pm-edge')) setSelection(null);
        }}
      >
        <defs>
          <marker id="pm-arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 z" fill="var(--vscode-foreground, #ccc)" opacity="0.7" />
          </marker>
          <marker id="pm-arrow-cycle" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
            <path d="M0,0 L10,5 L0,10 z" fill="#ffa726" />
          </marker>
        </defs>
        <g transform={`translate(${transform.tx},${transform.ty}) scale(${transform.scale})`}>
          {layout.rings
            .filter((r) => r.radius > 0)
            .map((r) => (
              <g key={`ring-${r.tier}`} className="pm-ring">
                <circle r={r.radius} fill="none" stroke={TIER_COLORS[r.tier] ?? '#666'} strokeOpacity={0.18} strokeDasharray="4 6" />
                <text y={-r.radius - 8} textAnchor="middle" className="pm-ring-label" fill={TIER_COLORS[r.tier] ?? '#888'}>
                  {ringLabel(r.tier)}
                </text>
              </g>
            ))}

          {visible.edges.map((e) => (
            <EdgeShape
              key={e.id}
              edge={e}
              from={nodeById.get(e.source)}
              to={nodeById.get(e.target)}
              positions={layout.positions}
              curved={reverseEdges.has(`${e.source}->${e.target}`)}
              maxCount={maxCount}
              dimmed={!!focusNeighbors && !(focusNeighbors.has(e.source) && focusNeighbors.has(e.target) && (e.source === focusId || e.target === focusId))}
              selected={selection?.type === 'edge' && selection.id === e.id}
              onSelect={() => setSelection({ type: 'edge', id: e.id })}
            />
          ))}

          {visible.nodes.map((n) => {
            const pos = layout.positions[n.id];
            if (!pos) return null;
            return (
              <NodeShape
                key={n.id}
                node={n}
                x={pos.x}
                y={pos.y}
                dimmed={!!focusNeighbors && !focusNeighbors.has(n.id)}
                selected={selection?.type === 'node' && selection.id === n.id}
                onSelect={() => setSelection({ type: 'node', id: n.id })}
                onHover={(h) => setHovered(h ? n.id : null)}
                onOpen={() => {
                  const target = n.filePath ?? n.files[0];
                  if (target) onOpenFile(target);
                }}
              />
            );
          })}
        </g>
      </svg>

      <div className="pm-legend">
        {model.tiers
          .filter((t) => visible.nodes.some((n) => n.tier === t.tier))
          .map((t) => (
            <span key={t.tier} className="pm-legend-item" title={t.description}>
              <span className="pm-legend-dot" style={{ background: TIER_COLORS[t.tier] }} />
              {t.label}
            </span>
          ))}
      </div>

      {(selectedNode || selectedEdge) && (
        <ProjectMapInspector
          model={model}
          node={selectedNode}
          edge={selectedEdge}
          onClose={() => setSelection(null)}
          onOpenFile={onOpenFile}
          onSelectNode={(id) => setSelection({ type: 'node', id })}
          onSelectEdge={(id) => setSelection({ type: 'edge', id })}
        />
      )}
    </div>
  );
};

const NodeShape: React.FC<{
  node: ProjectMapNodeModel;
  x: number;
  y: number;
  dimmed: boolean;
  selected: boolean;
  onSelect: () => void;
  onHover: (hovering: boolean) => void;
  onOpen: () => void;
}> = ({ node, x, y, dimmed, selected, onSelect, onHover, onOpen }) => {
  const r = nodeVisualRadius(node);
  const color = TIER_COLORS[node.tier] ?? '#888';
  const inCycle = node.cycleGroup !== undefined;
  return (
    <g
      className={`pm-node ${dimmed ? 'is-dimmed' : ''} ${selected ? 'is-selected' : ''}`}
      transform={`translate(${x},${y})`}
      onClick={(e) => {
        e.stopPropagation();
        onSelect();
      }}
      onDoubleClick={(e) => {
        e.stopPropagation();
        onOpen();
      }}
      onMouseEnter={() => onHover(true)}
      onMouseLeave={() => onHover(false)}
    >
      <title>{`${node.name}\n${node.isResource ? node.resourceKind : node.tierLabel}${node.tierReason ? `\n${node.tierReason}` : ''}`}</title>
      {node.isResource ? (
        <rect x={-r} y={-r} width={r * 2} height={r * 2} rx={5} fill={color} fillOpacity={0.22} stroke={color} strokeWidth={selected ? 3 : 1.5} />
      ) : (
        <circle
          r={r}
          fill={color}
          fillOpacity={0.25}
          stroke={inCycle ? '#ffa726' : color}
          strokeWidth={selected ? 3.5 : inCycle ? 2.5 : 1.5}
          strokeDasharray={inCycle ? '5 3' : undefined}
        />
      )}
      <text className="pm-node-badge" textAnchor="middle" dy="0.35em">
        {node.isResource ? resourceBadge(node.resourceKind) : node.fileCount}
      </text>
      <text className="pm-node-label" textAnchor="middle" y={r + 14}>
        {node.name.length > 26 ? `${node.name.slice(0, 25)}…` : node.name}
      </text>
    </g>
  );
};

const EdgeShape: React.FC<{
  edge: ProjectMapEdgeModel;
  from?: ProjectMapNodeModel;
  to?: ProjectMapNodeModel;
  positions: Record<string, { x: number; y: number }>;
  curved: boolean;
  maxCount: number;
  dimmed: boolean;
  selected: boolean;
  onSelect: () => void;
}> = ({ edge, from, to, positions, curved, maxCount, dimmed, selected, onSelect }) => {
  const a = positions[edge.source];
  const b = positions[edge.target];
  if (!a || !b || !from || !to) return null;
  const d = edgePath(a.x, a.y, b.x, b.y, nodeVisualRadius(from), nodeVisualRadius(to), curved);
  const width = 1 + (Math.log(1 + edge.count) / Math.log(1 + maxCount)) * 4;
  const cls = `pm-edge ${edge.isCircular ? 'is-circular' : ''} ${edge.isResourceLink ? 'is-resource' : ''} ${dimmed ? 'is-dimmed' : ''} ${selected ? 'is-selected' : ''}`;
  return (
    <g
      className={cls}
      onClick={(e) => {
        e.stopPropagation();
        onSelect();
      }}
    >
      <title>{`${from.name} → ${to.name}: ${edge.count} reference(s)${edge.isCircular ? ' (cycle)' : ''}`}</title>
      <path d={d} className="pm-edge-hit" />
      <path d={d} className="pm-edge-line" strokeWidth={selected ? width + 1.5 : width} markerEnd={edge.isCircular ? 'url(#pm-arrow-cycle)' : 'url(#pm-arrow)'} />
    </g>
  );
};
