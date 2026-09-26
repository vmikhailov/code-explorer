import React, { useState, useMemo } from 'react';
import { ClassifiedC1Node, ClassifiedC1Edge } from './C1SystemContextView';

export interface C1MatrixViewProps {
  nodes: ClassifiedC1Node[];
  edges: ClassifiedC1Edge[];
  selectedNodeId?: string;
  onSelectNode?: (node: any) => void;
  onDrillDownToC2?: (projectName: string) => void;
}

export const C1MatrixView: React.FC<C1MatrixViewProps> = ({
  nodes,
  edges,
  selectedNodeId,
  onSelectNode,
  onDrillDownToC2,
}) => {
  const [filterQuery, setFilterQuery] = useState<string>('');
  const [hoveredCell, setHoveredCell] = useState<{ rowId: string; colId: string; count: number } | null>(null);

  // Group nodes: Apps, Services, External, sorted alphabetically
  const orderedNodes = useMemo(() => {
    const apps = nodes.filter((n) => n.category === 'app');
    const svcs = nodes.filter((n) => n.category === 'service');
    const exts = nodes.filter((n) => n.category === 'external');

    const sortFn = (a: ClassifiedC1Node, b: ClassifiedC1Node) => a.name.localeCompare(b.name);
    apps.sort(sortFn);
    svcs.sort(sortFn);
    exts.sort(sortFn);

    const full = [...apps, ...svcs, ...exts];
    if (!filterQuery.trim()) return full;
    const lower = filterQuery.toLowerCase();
    return full.filter((n) => n.name.toLowerCase().includes(lower) || n.displayName.toLowerCase().includes(lower));
  }, [nodes, filterQuery]);

  // Build NxN lookup matrix: map[source][target] = count
  const matrixMap = useMemo(() => {
    const map = new Map<string, Map<string, number>>();
    for (const n of nodes) {
      map.set(n.id, new Map());
    }
    for (const e of edges) {
      if (!map.has(e.source)) map.set(e.source, new Map());
      map.get(e.source)!.set(e.target, e.count);
    }
    return map;
  }, [nodes, edges]);

  // Find cyclic dependency pairs (A calls B and B calls A)
  const cyclicPairs = useMemo(() => {
    const set = new Set<string>();
    for (const e of edges) {
      const reverseCount = matrixMap.get(e.target)?.get(e.source) || 0;
      if (reverseCount > 0 && e.source !== e.target) {
        set.add(`${e.source}->${e.target}`);
        set.add(`${e.target}->${e.source}`);
      }
    }
    return set;
  }, [edges, matrixMap]);

  const totalPossible = orderedNodes.length * (orderedNodes.length - 1);
  const density = totalPossible > 0 ? ((edges.length / totalPossible) * 100).toFixed(1) : '0';

  return (
    <div className="c1-matrix-container">
      {/* Top Controls & Metrics Bar */}
      <div className="c1-matrix-topbar">
        <div className="c1-matrix-search-wrap">
          <span className="search-icon">🔍</span>
          <input
            type="text"
            className="c1-matrix-input"
            placeholder="Filter matrix rows & columns..."
            value={filterQuery}
            onChange={(e) => setFilterQuery(e.target.value)}
          />
          {filterQuery && (
            <button className="c1-matrix-clear-btn" onClick={() => setFilterQuery('')}>
              ✕
            </button>
          )}
        </div>

        <div className="c1-matrix-metrics">
          <span className="metric-pill">
            Services: <strong>{orderedNodes.length}</strong>
          </span>
          <span className="metric-pill">
            Links: <strong>{edges.length}</strong>
          </span>
          <span className={`metric-pill ${cyclicPairs.size > 0 ? 'warning' : ''}`}>
            Cyclic Pairs: <strong>{cyclicPairs.size / 2}</strong>
          </span>
          <span className="metric-pill">
            Density: <strong>{density}%</strong>
          </span>
        </div>

        {/* Hover inspection banner */}
        <div className="c1-matrix-banner">
          {hoveredCell ? (
            <div className="hover-active-info">
              <span className="caller-name">
                {nodes.find((n) => n.id === hoveredCell.rowId)?.displayName || hoveredCell.rowId}
              </span>
              <span className="arrow-sym">➔ calls ({hoveredCell.count}) ➔</span>
              <span className="callee-name">
                {nodes.find((n) => n.id === hoveredCell.colId)?.displayName || hoveredCell.colId}
              </span>
              {cyclicPairs.has(`${hoveredCell.rowId}->${hoveredCell.colId}`) && (
                <span className="cyclic-warn-badge">⚠️ Cyclic Feedback Loop</span>
              )}
            </div>
          ) : (
            <span className="hover-idle-text">Hover any matrix cell to inspect dependency relationship</span>
          )}
        </div>
      </div>

      {/* Matrix Table Scrollable Wrapper */}
      <div className="c1-matrix-table-wrap">
        <table className="c1-matrix-table">
          <thead>
            <tr>
              <th className="corner-th">Source \ Target</th>
              {orderedNodes.map((n, idx) => (
                <th
                  key={`th-${n.id}`}
                  className={`col-th cat-${n.category} ${hoveredCell?.colId === n.id ? 'is-highlighted-col' : ''}`}
                  title={`${idx + 1}. ${n.displayName || n.name} (${n.category.toUpperCase()})`}
                >
                  <div className="col-th-inner">
                    <span className="col-idx">{idx + 1}</span>
                    <span className="col-name">{n.displayName || n.name}</span>
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {orderedNodes.map((rowNode, rIdx) => {
              const isRowHighlighted = hoveredCell?.rowId === rowNode.id;
              return (
                <tr key={`tr-${rowNode.id}`} className={isRowHighlighted ? 'is-highlighted-row' : ''}>
                  {/* Row Header */}
                  <th
                    className={`row-th cat-${rowNode.category} ${isRowHighlighted ? 'is-highlighted-row-th' : ''}`}
                    title={`${rowNode.displayName || rowNode.name} (${rowNode.category.toUpperCase()})`}
                    onClick={() => onSelectNode?.(rowNode.rawNode)}
                  >
                    <span className="row-idx">{rIdx + 1}</span>
                    <span className={`c1-laconic-badge laconic-badge-${rowNode.category === 'app' ? 'app' : rowNode.category === 'external' ? 'ext' : 'svc'}`}>
                      {rowNode.category.toUpperCase().slice(0, 3)}
                    </span>
                    <span className="row-name">{rowNode.displayName || rowNode.name}</span>
                  </th>

                  {/* Cells */}
                  {orderedNodes.map((colNode) => {
                    const isDiagonal = rowNode.id === colNode.id;
                    const count = matrixMap.get(rowNode.id)?.get(colNode.id) || 0;
                    const isCyclic = cyclicPairs.has(`${rowNode.id}->${colNode.id}`);
                    const isCellHovered = hoveredCell?.rowId === rowNode.id && hoveredCell?.colId === colNode.id;

                    if (isDiagonal) {
                      return <td key={`cell-${rowNode.id}-${colNode.id}`} className="cell-diagonal" title="Self (diagonal)" />;
                    }

                    if (count === 0) {
                      return (
                        <td
                          key={`cell-${rowNode.id}-${colNode.id}`}
                          className={`cell-empty ${hoveredCell?.rowId === rowNode.id || hoveredCell?.colId === colNode.id ? 'crosshair-dim' : ''}`}
                        >
                          ·
                        </td>
                      );
                    }

                    const heatClass =
                      count >= 5 ? 'heat-high' : count >= 3 ? 'heat-med' : 'heat-low';

                    return (
                      <td
                        key={`cell-${rowNode.id}-${colNode.id}`}
                        className={`cell-data ${heatClass} ${isCyclic ? 'is-cyclic' : ''} ${isCellHovered ? 'is-hovered' : ''}`}
                        onMouseEnter={() => setHoveredCell({ rowId: rowNode.id, colId: colNode.id, count })}
                        onMouseLeave={() => setHoveredCell(null)}
                        title={`${rowNode.name} calls ${colNode.name} (${count} times)${isCyclic ? ' [CYCLIC]' : ''}`}
                      >
                        {isCyclic && <span className="cyclic-star">⚠️</span>}
                        <span className="cell-num">{count}</span>
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
