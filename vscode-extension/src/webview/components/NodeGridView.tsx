import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { GraphData, GraphNode } from '../../../../proto/types';
import { ViewMode } from '../commands';

export interface NodeCategorySelection {
  kind: string;
  layerTitle?: string;
}

export interface NodeDto {
  id: string;
  name: string;
  kind: string;
  displayName?: string;
  filePath?: string;
  lineStart?: number;
  lineEnd?: number;
  properties?: Record<string, string>;
}

interface NodeGridViewProps {
  category: NodeCategorySelection | null;
  graph: GraphData | null;
  serverHttpUrl?: string;
  onOpenFile: (filePath: string, lineStart?: number) => void;
  onFocusInDiagram?: (nodeId: string, kind?: string) => void;
  onSelectNode?: (node: GraphNode) => void;
  onSwitchView?: (mode: ViewMode) => void;
}

export const NodeGridView: React.FC<NodeGridViewProps> = ({
  category,
  graph,
  serverHttpUrl,
  onOpenFile,
  onFocusInDiagram,
  onSelectNode,
  onSwitchView,
}) => {
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [page, setPage] = useState<number>(0);
  const [pageSize, setPageSize] = useState<number>(50);
  const [sortField, setSortField] = useState<'name' | 'kind' | 'filePath' | 'line'>('name');
  const [sortAsc, setSortAsc] = useState<boolean>(true);

  const [remoteNodes, setRemoteNodes] = useState<NodeDto[] | null>(null);
  const [remoteTotal, setRemoteTotal] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const currentKind = category?.kind || '';
  const currentLayer = category?.layerTitle || 'Graph Nodes';

  // Fetch from server /api/nodes when category or paging changes
  const fetchRemoteNodes = useCallback(async () => {
    if (!serverHttpUrl || !currentKind) return;
    setLoading(true);
    setError(null);
    try {
      const url = `${serverHttpUrl}/api/nodes?kind=${encodeURIComponent(currentKind)}&offset=${page * pageSize}&limit=${pageSize}&search=${encodeURIComponent(searchTerm)}`;
      const res = await fetch(url);
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      }
      const data = await res.json();
      setRemoteNodes(data.nodes || []);
      setRemoteTotal(data.total || 0);
    } catch (err: any) {
      console.warn('[NodeGridView] Remote fetch failed, falling back to local graph:', err.message);
      setError(err.message);
      setRemoteNodes(null);
    } finally {
      setLoading(false);
    }
  }, [serverHttpUrl, currentKind, page, pageSize, searchTerm]);

  useEffect(() => {
    setPage(0);
  }, [currentKind, searchTerm]);

  useEffect(() => {
    fetchRemoteNodes();
  }, [fetchRemoteNodes]);

  // Fallback: local graph filtering when server is offline or remoteNodes is null
  const localFilteredNodes = useMemo(() => {
    if (!graph?.nodes) return [];
    let list = graph.nodes;
    if (currentKind) {
      list = list.filter((n) => n.kind?.toLowerCase() === currentKind.toLowerCase());
    }
    if (searchTerm.trim()) {
      const q = searchTerm.toLowerCase();
      list = list.filter(
        (n) =>
          n.name?.toLowerCase().includes(q) ||
          n.displayName?.toLowerCase().includes(q) ||
          n.id?.toLowerCase().includes(q) ||
          n.filePath?.toLowerCase().includes(q) ||
          Object.values(n.properties || {}).some((val) => val?.toLowerCase().includes(q))
      );
    }

    list = [...list].sort((a, b) => {
      let cmp = 0;
      if (sortField === 'name') {
        cmp = (a.displayName || a.name || '').localeCompare(b.displayName || b.name || '');
      } else if (sortField === 'kind') {
        cmp = (a.kind || '').localeCompare(b.kind || '');
      } else if (sortField === 'filePath') {
        cmp = (a.filePath || '').localeCompare(b.filePath || '');
      } else if (sortField === 'line') {
        cmp = (a.lineStart || 0) - (b.lineStart || 0);
      }
      return sortAsc ? cmp : -cmp;
    });

    return list;
  }, [graph, currentKind, searchTerm, sortField, sortAsc]);

  // Determine active displayed rows and total count
  const isRemote = remoteNodes !== null && !error;
  const displayedNodes: (NodeDto | GraphNode)[] = isRemote
    ? remoteNodes
    : localFilteredNodes.slice(page * pageSize, (page + 1) * pageSize);
  const totalCount = isRemote ? remoteTotal : localFilteredNodes.length;
  const totalPages = Math.ceil(totalCount / pageSize);

  const handleSort = (field: 'name' | 'kind' | 'filePath' | 'line') => {
    if (sortField === field) {
      setSortAsc(!sortAsc);
    } else {
      setSortField(field);
      setSortAsc(true);
    }
  };

  const getKindBadgeClass = (kind: string) => {
    const k = kind?.toLowerCase() || '';
    if (k.includes('endpoint')) return 'badge-endpoint';
    if (k.includes('database') || k.includes('table')) return 'badge-db';
    if (k.includes('service') || k.includes('project')) return 'badge-service';
    if (k.includes('type') || k.includes('class')) return 'badge-type';
    if (k.includes('function') || k.includes('method')) return 'badge-fn';
    if (k.includes('topic')) return 'badge-topic';
    return 'badge-default';
  };

  const getKindIcon = (kind: string) => {
    switch (kind) {
      case 'Project':
        return '📦';
      case 'Endpoint':
        return '🌐';
      case 'Database':
        return '🗄️';
      case 'Table':
        return '📋';
      case 'Topic':
        return '📬';
      case 'EntryPoint':
        return '🚪';
      case 'ExternalService':
        return '☁️';
      case 'CloudService':
        return '⚡';
      case 'ApiInUse':
        return '🔌';
      case 'Query':
        return '🔍';
      case 'Type':
        return '🔷';
      case 'Function':
        return '⚡';
      case 'Member':
        return '🔹';
      case 'File':
        return '📄';
      case 'Folder':
        return '📁';
      default:
        return '🔹';
    }
  };

  const formatKeyDetails = (node: NodeDto | GraphNode): string => {
    const props = node.properties || {};
    const parts: string[] = [];

    if (props.http_method || props.method) {
      parts.push(`[${props.http_method || props.method}]`);
    }
    if (props.route_template || props.route) {
      parts.push(props.route_template || props.route);
    }
    if (props.db_type || props.engine) {
      parts.push(`${props.db_type || props.engine}`);
    }
    if (props.broker_type) {
      parts.push(`broker: ${props.broker_type}`);
    }
    if (props.framework) {
      parts.push(`framework: ${props.framework}`);
    }
    if (props.type_kind) {
      parts.push(`type: ${props.type_kind}`);
    }
    if (props.return_type) {
      parts.push(`returns: ${props.return_type}`);
    }

    if (parts.length > 0) return parts.join(' ');
    // Fallback: pick first couple of properties
    const entries = Object.entries(props).slice(0, 2);
    return entries.map(([k, v]) => `${k}: ${v}`).join(' • ');
  };

  return (
    <div className="node-grid-container">
      {/* Top Header & Toolbar */}
      <header className="node-grid-header">
        <div className="header-left">
          <div className="header-breadcrumbs">
            <span className="breadcrumb-layer">{currentLayer}</span>
            <span className="breadcrumb-separator">/</span>
            <span className="breadcrumb-kind">
              {getKindIcon(currentKind)} {currentKind || 'All Nodes'}
            </span>
          </div>
          <span className="total-badge">{totalCount.toLocaleString()} items</span>
        </div>

        <div className="header-right">
          <div className="grid-search-box">
            <span className="search-icon">🔍</span>
            <input
              type="text"
              placeholder={`Filter ${currentKind || 'nodes'}...`}
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="grid-search-input"
            />
            {searchTerm && (
              <button className="clear-search-btn" onClick={() => setSearchTerm('')} title="Clear filter">
                ✕
              </button>
            )}
          </div>

          <button
            className="action-btn secondary"
            onClick={fetchRemoteNodes}
            title="Refresh grid data"
            disabled={loading}
          >
            {loading ? '⏳' : '🔄'} Refresh
          </button>

          {onSwitchView && (
            <button
              className="action-btn primary"
              onClick={() => onSwitchView('c1')}
              title="Return to C1 System Context Diagram"
            >
              🌐 Diagram View
            </button>
          )}
        </div>
      </header>

      {/* Main Table Content */}
      <div className="node-grid-table-wrapper">
        <table className="node-grid-table">
          <thead>
            <tr>
              <th className="th-name" onClick={() => handleSort('name')}>
                Symbol / Name {sortField === 'name' ? (sortAsc ? '▲' : '▼') : ''}
              </th>
              <th className="th-kind" onClick={() => handleSort('kind')}>
                Kind {sortField === 'kind' ? (sortAsc ? '▲' : '▼') : ''}
              </th>
              <th className="th-location" onClick={() => handleSort('filePath')}>
                Location {sortField === 'filePath' ? (sortAsc ? '▲' : '▼') : ''}
              </th>
              <th className="th-details">Key Details</th>
              <th className="th-actions">Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading && displayedNodes.length === 0 ? (
              <tr>
                <td colSpan={5} className="grid-loading-cell">
                  <div className="grid-loading-spinner">⚡ Loading {currentKind || 'nodes'}...</div>
                </td>
              </tr>
            ) : displayedNodes.length === 0 ? (
              <tr>
                <td colSpan={5} className="grid-empty-cell">
                  <div className="empty-icon">🔍</div>
                  <div className="empty-title">No matching nodes found</div>
                  <div className="empty-desc">
                    {searchTerm
                      ? `No ${currentKind || 'nodes'} matched "${searchTerm}". Try a different search.`
                      : `No nodes of kind "${currentKind}" indexed in this workspace.`}
                  </div>
                </td>
              </tr>
            ) : (
              displayedNodes.map((node) => {
                const name = node.displayName || node.name || node.id;
                const details = formatKeyDetails(node);
                const hasFile = Boolean(node.filePath);

                return (
                  <tr key={node.id} className="node-grid-row">
                    <td className="td-name">
                      <span className="row-icon">{getKindIcon(node.kind)}</span>
                      <span className="row-name" title={node.id}>
                        {name}
                      </span>
                    </td>

                    <td className="td-kind">
                      <span className={`kind-tag ${getKindBadgeClass(node.kind)}`}>{node.kind}</span>
                    </td>

                    <td className="td-location">
                      {hasFile ? (
                        <span
                          className="file-link"
                          onClick={() => onOpenFile(node.filePath!, node.lineStart)}
                          title={`Open ${node.filePath}:${node.lineStart || 1} in editor`}
                        >
                          {node.filePath}:{node.lineStart || 1}
                        </span>
                      ) : (
                        <span className="location-none">—</span>
                      )}
                    </td>

                    <td className="td-details">
                      <span className="details-text" title={details}>
                        {details || '—'}
                      </span>
                    </td>

                    <td className="td-actions">
                      <div className="row-action-buttons">
                        {hasFile && (
                          <button
                            className="mini-btn"
                            onClick={() => onOpenFile(node.filePath!, node.lineStart)}
                            title="Open source code at declaration line"
                          >
                            📄 Code
                          </button>
                        )}
                        {onFocusInDiagram && (
                          <button
                            className="mini-btn highlight"
                            onClick={() => onFocusInDiagram(node.id, node.kind)}
                            title="Locate and focus this node in diagram"
                          >
                            🎯 Focus
                          </button>
                        )}
                        {onSelectNode && (
                          <button
                            className="mini-btn"
                            onClick={() => onSelectNode(node as GraphNode)}
                            title="Inspect full properties in drawer"
                          >
                            ℹ️ Info
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination Footer */}
      <footer className="node-grid-footer">
        <div className="footer-left">
          <span>
            Showing {totalCount > 0 ? page * pageSize + 1 : 0} –{' '}
            {Math.min((page + 1) * pageSize, totalCount)} of {totalCount.toLocaleString()}
          </span>
        </div>

        <div className="footer-center">
          <button
            className="page-btn"
            disabled={page === 0}
            onClick={() => setPage(0)}
            title="First page"
          >
            ««
          </button>
          <button
            className="page-btn"
            disabled={page === 0}
            onClick={() => setPage((p) => Math.max(0, p - 1))}
            title="Previous page"
          >
            ‹ Prev
          </button>
          <span className="page-indicator">
            Page {totalPages > 0 ? page + 1 : 0} of {totalPages || 1}
          </span>
          <button
            className="page-btn"
            disabled={page + 1 >= totalPages}
            onClick={() => setPage((p) => Math.min(totalPages - 1, p + 1))}
            title="Next page"
          >
            Next ›
          </button>
          <button
            className="page-btn"
            disabled={page + 1 >= totalPages}
            onClick={() => setPage(totalPages - 1)}
            title="Last page"
          >
            »»
          </button>
        </div>

        <div className="footer-right">
          <label className="page-size-label">Rows per page:</label>
          <select
            value={pageSize}
            onChange={(e) => {
              setPageSize(Number(e.target.value));
              setPage(0);
            }}
            className="page-size-select"
          >
            <option value={25}>25</option>
            <option value={50}>50</option>
            <option value={100}>100</option>
            <option value={250}>250</option>
          </select>
        </div>
      </footer>
    </div>
  );
};
