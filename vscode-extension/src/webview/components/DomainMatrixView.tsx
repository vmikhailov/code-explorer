/**
 * DomainMatrixView.tsx
 *
 * An interactive, high-density Dependency Matrix View for DomainArchitectureView.
 * Visualizes Caller (Rows) vs Callee (Columns) with cell badges for service calls,
 * DB access, and event flows. Highlights circular dependencies and indirect calls.
 */

import React, { useState, useMemo } from 'react';
import { DomainNodeDetail } from './DomainArchitectureView';

export interface DomainMatrixEdge {
  source: string;
  target: string;
  category: string;
  count: number;
  isTransitive?: string;
  label?: string;
}

export interface DomainMatrixViewProps {
  nodes: DomainNodeDetail[];
  edges: DomainMatrixEdge[];
  selectedNodeId?: string;
  onSelectNode?: (node: DomainNodeDetail | null) => void;
  onOpenFile?: (filePath: string, line?: number) => void;
}

export const DomainMatrixView: React.FC<DomainMatrixViewProps> = ({
  nodes,
  edges,
  selectedNodeId,
  onSelectNode,
  onOpenFile,
}) => {
  const [filterQuery, setFilterQuery] = useState<string>('');
  const [hoveredCell, setHoveredCell] = useState<{ rowId: string; colId: string } | null>(null);

  // Filter nodes by search query
  const filteredNodes = useMemo(() => {
    // Sort: Ingress first, then Services, Workers, Topics, DBs
    const orderKind: Record<string, number> = {
      Ingress: 0,
      Service: 1,
      Worker: 2,
      Topic: 3,
      Database: 4,
      External: 5,
    };

    const sorted = [...nodes].sort((a, b) => {
      const kA = orderKind[a.kind] ?? 9;
      const kB = orderKind[b.kind] ?? 9;
      if (kA !== kB) return kA - kB;
      return a.displayName.localeCompare(b.displayName);
    });

    if (!filterQuery.trim()) return sorted;
    const q = filterQuery.toLowerCase();
    return sorted.filter(
      (n) =>
        n.name.toLowerCase().includes(q) ||
        n.displayName.toLowerCase().includes(q) ||
        n.kind.toLowerCase().includes(q)
    );
  }, [nodes, filterQuery]);

  // Lookup map: matrix[source][target] = edge
  const matrixMap = useMemo(() => {
    const map = new Map<string, Map<string, DomainMatrixEdge>>();
    for (const n of nodes) {
      map.set(n.id, new Map());
    }
    for (const e of edges) {
      if (!map.has(e.source)) map.set(e.source, new Map());
      map.get(e.source)!.set(e.target, e);
    }
    return map;
  }, [nodes, edges]);

  // Detect cyclic dependencies (A -> B and B -> A)
  const cyclicPairs = useMemo(() => {
    const set = new Set<string>();
    for (const e of edges) {
      const reverse = matrixMap.get(e.target)?.get(e.source);
      if (reverse && e.source !== e.target) {
        set.add(`${e.source}->${e.target}`);
        set.add(`${e.target}->${e.source}`);
      }
    }
    return set;
  }, [edges, matrixMap]);

  const totalPossible = filteredNodes.length * (filteredNodes.length - 1);
  const density = totalPossible > 0 ? ((edges.length / totalPossible) * 100).toFixed(1) : '0';

  const getNodeKindColor = (kind: string) => {
    switch (kind) {
      case 'Ingress':
        return '#38bdf8';
      case 'Service':
        return '#4ade80';
      case 'Worker':
        return '#fb923c';
      case 'Topic':
        return '#fbbf24';
      case 'Database':
        return '#c084fc';
      case 'External':
        return '#34d399';
      default:
        return '#94a3b8';
    }
  };

  const getEdgeCategoryColor = (cat: string) => {
    switch (cat) {
      case 'service_call':
        return '#38bdf8';
      case 'database':
        return '#c084fc';
      case 'messaging':
        return '#fbbf24';
      case 'external':
        return '#34d399';
      default:
        return '#94a3b8';
    }
  };

  return (
    <div
      className="domain-matrix-container"
      style={{
        position: 'absolute',
        inset: 0,
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: '#090d16',
        color: '#e2e8f0',
        zIndex: 1,
        overflow: 'hidden',
      }}
    >
      {/* Top Metrics & Filter Bar */}
      <div
        className="domain-matrix-topbar"
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: '12px',
          padding: '8px 16px',
          backgroundColor: '#0f172a',
          borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
          fontSize: '12px',
          flexShrink: 0,
        }}
      >
        <div style={{ position: 'relative', width: '260px' }}>
          <input
            type="text"
            placeholder="Filter matrix rows & columns..."
            value={filterQuery}
            onChange={(e) => setFilterQuery(e.target.value)}
            style={{
              width: '100%',
              backgroundColor: '#1e293b',
              border: '1px solid rgba(255, 255, 255, 0.12)',
              borderRadius: '6px',
              padding: '4px 10px',
              color: '#e2e8f0',
              fontSize: '11px',
              outline: 'none',
            }}
          />
          {filterQuery && (
            <button
              onClick={() => setFilterQuery('')}
              style={{
                position: 'absolute',
                right: '8px',
                top: '50%',
                transform: 'translateY(-50%)',
                background: 'none',
                border: 'none',
                color: '#94a3b8',
                cursor: 'pointer',
              }}
            >
              ✕
            </button>
          )}
        </div>

        <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
          <span className="hud-stat-pill">Entities: <strong>{filteredNodes.length}</strong></span>
          <span className="hud-stat-pill">Connections: <strong>{edges.length}</strong></span>
          {cyclicPairs.size > 0 && (
            <span
              className="hud-stat-pill"
              style={{ borderColor: 'rgba(239, 68, 68, 0.4)', color: '#f87171' }}
              title="Circular dependency loops detected"
            >
              ⚠️ Cyclic Pairs: <strong>{cyclicPairs.size / 2}</strong>
            </span>
          )}
          <span className="hud-stat-pill">Density: <strong>{density}%</strong></span>
        </div>

        <div style={{ marginLeft: 'auto', display: 'flex', gap: '12px', fontSize: '11px', opacity: 0.85 }}>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
            <span style={{ width: 8, height: 8, borderRadius: 2, background: '#38bdf8' }} /> Service Call
          </span>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
            <span style={{ width: 8, height: 8, borderRadius: 2, background: '#c084fc' }} /> Database
          </span>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
            <span style={{ width: 8, height: 8, borderRadius: 2, background: '#fbbf24' }} /> Event Topic
          </span>
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
            <span style={{ width: 10, height: 0, borderTop: '2px dashed #94a3b8' }} /> Indirect (via hidden)
          </span>
        </div>
      </div>

      {/* Matrix Grid Scrollable Table */}
      <div
        className="domain-matrix-table-wrap"
        style={{
          flex: 1,
          overflow: 'auto',
          position: 'relative',
        }}
      >
        <table
          style={{
            borderCollapse: 'collapse',
            fontSize: '11px',
            minWidth: '100%',
          }}
        >
          <thead>
            <tr>
              {/* Sticky Top-Left Corner */}
              <th
                style={{
                  position: 'sticky',
                  top: 0,
                  left: 0,
                  zIndex: 4,
                  backgroundColor: '#0f172a',
                  padding: '8px 12px',
                  borderRight: '1px solid rgba(255, 255, 255, 0.1)',
                  borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
                  textAlign: 'left',
                  minWidth: '200px',
                  maxWidth: '240px',
                }}
              >
                Caller (Row) \ Callee (Col)
              </th>

              {/* Column Headers (Callees) */}
              {filteredNodes.map((colNode) => {
                const isHoveredCol = hoveredCell?.colId === colNode.id;
                const isSelectedCol = selectedNodeId === colNode.id;
                const kindColor = getNodeKindColor(colNode.kind);

                return (
                  <th
                    key={colNode.id}
                    onClick={() => onSelectNode?.(colNode)}
                    title={`${colNode.displayName} (${colNode.kind}) - Click to inspect`}
                    style={{
                      position: 'sticky',
                      top: 0,
                      zIndex: 3,
                      backgroundColor: isHoveredCol ? '#1e293b' : '#0f172a',
                      padding: '8px 4px',
                      borderRight: '1px solid rgba(255, 255, 255, 0.06)',
                      borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
                      width: '36px',
                      minWidth: '36px',
                      maxWidth: '36px',
                      height: '140px',
                      verticalAlign: 'bottom',
                      cursor: 'pointer',
                      transition: 'background-color 0.15s ease',
                      borderTop: isSelectedCol ? `3px solid ${kindColor}` : 'none',
                    }}
                  >
                    <div
                      style={{
                        writingMode: 'vertical-rl',
                        transform: 'rotate(180deg)',
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        maxHeight: '120px',
                        color: isHoveredCol ? '#38bdf8' : '#cbd5e1',
                        fontWeight: isSelectedCol ? 700 : 500,
                        textAlign: 'left',
                        paddingLeft: '4px',
                      }}
                    >
                      <span style={{ color: kindColor, marginRight: '4px' }}>•</span>
                      {colNode.displayName}
                    </div>
                  </th>
                );
              })}
            </tr>
          </thead>

          <tbody>
            {filteredNodes.map((rowNode, rIdx) => {
              const isHoveredRow = hoveredCell?.rowId === rowNode.id;
              const isSelectedRow = selectedNodeId === rowNode.id;
              const rowKindColor = getNodeKindColor(rowNode.kind);

              return (
                <tr
                  key={rowNode.id}
                  style={{
                    backgroundColor: isSelectedRow
                      ? 'rgba(56, 189, 248, 0.08)'
                      : isHoveredRow
                      ? 'rgba(30, 41, 59, 0.5)'
                      : rIdx % 2 === 0
                      ? 'transparent'
                      : 'rgba(255, 255, 255, 0.015)',
                  }}
                >
                  {/* Sticky Row Header (Caller) */}
                  <th
                    onClick={() => onSelectNode?.(rowNode)}
                    title={`${rowNode.displayName} (${rowNode.kind}) - Click to inspect`}
                    style={{
                      position: 'sticky',
                      left: 0,
                      zIndex: 2,
                      backgroundColor: isHoveredRow ? '#1e293b' : '#0f172a',
                      padding: '6px 12px',
                      borderRight: '1px solid rgba(255, 255, 255, 0.1)',
                      borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
                      textAlign: 'left',
                      whiteSpace: 'nowrap',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      cursor: 'pointer',
                      borderLeft: isSelectedRow ? `3px solid ${rowKindColor}` : 'none',
                    }}
                  >
                    <span style={{ color: rowKindColor, marginRight: '6px' }}>•</span>
                    <span style={{ fontWeight: isSelectedRow ? 700 : 500 }}>{rowNode.displayName}</span>
                    <span style={{ opacity: 0.5, fontSize: '9px', marginLeft: '6px' }}>({rowNode.kind})</span>
                  </th>

                  {/* Matrix Cells */}
                  {filteredNodes.map((colNode) => {
                    const edge = matrixMap.get(rowNode.id)?.get(colNode.id);
                    const isSelf = rowNode.id === colNode.id;
                    const isCyclic = cyclicPairs.has(`${rowNode.id}->${colNode.id}`);
                    const isHovered = hoveredCell?.rowId === rowNode.id && hoveredCell?.colId === colNode.id;
                    const isHoveredCross = hoveredCell?.rowId === rowNode.id || hoveredCell?.colId === colNode.id;

                    if (isSelf) {
                      return (
                        <td
                          key={colNode.id}
                          style={{
                            backgroundColor: 'rgba(15, 23, 42, 0.6)',
                            borderRight: '1px solid rgba(255, 255, 255, 0.04)',
                            borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
                            textAlign: 'center',
                          }}
                        >
                          <span style={{ opacity: 0.15 }}>—</span>
                        </td>
                      );
                    }

                    if (!edge) {
                      return (
                        <td
                          key={colNode.id}
                          onMouseEnter={() => setHoveredCell({ rowId: rowNode.id, colId: colNode.id })}
                          onMouseLeave={() => setHoveredCell(null)}
                          style={{
                            backgroundColor: isHoveredCross ? 'rgba(56, 189, 248, 0.03)' : 'transparent',
                            borderRight: '1px solid rgba(255, 255, 255, 0.04)',
                            borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
                          }}
                        />
                      );
                    }

                    const isTrans = edge.isTransitive === 'true';
                    const edgeColor = getEdgeCategoryColor(edge.category);

                    return (
                      <td
                        key={colNode.id}
                        onMouseEnter={() => setHoveredCell({ rowId: rowNode.id, colId: colNode.id })}
                        onMouseLeave={() => setHoveredCell(null)}
                        title={`${rowNode.displayName} -> ${colNode.displayName}\nType: ${edge.category} (${edge.count} calls)\n${isTrans ? 'Indirect via hidden' : 'Direct'}${isCyclic ? '\n⚠️ Circular Dependency!' : ''}`}
                        style={{
                          backgroundColor: isHovered
                            ? 'rgba(56, 189, 248, 0.25)'
                            : isCyclic
                            ? 'rgba(239, 68, 68, 0.18)'
                            : isHoveredCross
                            ? 'rgba(56, 189, 248, 0.08)'
                            : 'rgba(255, 255, 255, 0.02)',
                          borderRight: '1px solid rgba(255, 255, 255, 0.04)',
                          borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
                          textAlign: 'center',
                          padding: '2px',
                        }}
                      >
                        <div
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            width: '24px',
                            height: '20px',
                            borderRadius: '4px',
                            fontSize: '10px',
                            fontWeight: 700,
                            backgroundColor: isCyclic ? 'rgba(239, 68, 68, 0.3)' : `${edgeColor}25`,
                            color: isCyclic ? '#fca5a5' : edgeColor,
                            border: isTrans
                              ? `1.5px dashed ${edgeColor}`
                              : `1px solid ${edgeColor}60`,
                          }}
                        >
                          {edge.count > 1 ? edge.count : '✓'}
                        </div>
                      </td>
                    );
                  })}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
};
