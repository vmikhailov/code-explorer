import React, { useMemo, useState, useRef, useEffect } from 'react';

export interface ToolbarProps {
  viewMode: 'semantic' | 'flow' | 'layers' | 'full';
  onViewModeChange: (mode: 'semantic' | 'flow' | 'layers' | 'full') => void;
  allProjects: string[];
  projectPaths?: Record<string, string>;
  selectedProject: string;
  onSelectProject: (project: string) => void;
  connectionStatus: 'connecting' | 'connected' | 'disconnected';
  onFitView?: () => void;
  onRefresh: () => void;
  cypherQuery: string;
  onCypherQueryChange: (query: string) => void;
  onRunCypher: () => void;
  showTests: boolean;
  onToggleShowTests: () => void;
  canGoBack: boolean;
  canGoForward: boolean;
  onGoBack: () => void;
  onGoForward: () => void;
  undoDescription?: string | null;
  redoDescription?: string | null;
  groupLayers: boolean;
  onToggleGroupLayers: () => void;
  isScanning?: boolean;
  scanProgress?: { phase: string; percentage: number; currentFile?: string; totalFiles?: number; processedFiles?: number } | null;
  onTriggerScan?: (clear?: boolean) => void;
  graphStats?: { totalNodes: number; totalEdges: number; serverVersion?: string } | null;
}

export const Toolbar: React.FC<ToolbarProps> = ({
  viewMode,
  onViewModeChange,
  allProjects,
  projectPaths,
  selectedProject,
  onSelectProject,
  connectionStatus,
  onRefresh,
  cypherQuery,
  onCypherQueryChange,
  onRunCypher,
  showTests,
  onToggleShowTests,
  canGoBack,
  canGoForward,
  onGoBack,
  onGoForward,
  undoDescription,
  redoDescription,
  groupLayers,
  onToggleGroupLayers,
  isScanning,
  scanProgress,
  onTriggerScan,
  graphStats,
}) => {
  const uniqueProjects = useMemo(() => {
    const seen = new Set<string>();
    const result: string[] = [];
    for (const p of allProjects) {
      const lower = p.toLowerCase();
      if (!seen.has(lower)) {
        seen.add(lower);
        result.push(p);
      }
    }
    return result;
  }, [allProjects]);

  const getProjectFolder = (projectName: string): string => {
    let rawPath =
      projectPaths?.[projectName] ||
      projectPaths?.[projectName.toLowerCase()] ||
      '';

    if (!rawPath) {
      return 'Projects';
    }

    // Normalize slashes and trim outer slashes
    rawPath = rawPath.replace(/\\/g, '/').replace(/^\/+|\/+$/g, '');
    const parts = rawPath.split('/').filter(Boolean);

    // If last component contains a file extension (e.g. .csproj, package.json), remove it
    if (parts.length > 1 && (parts[parts.length - 1].includes('.') || parts[parts.length - 1].endsWith('proj'))) {
      parts.pop();
    }

    // If last folder matches the project name, take its parent directory
    if (parts.length > 1 && parts[parts.length - 1].toLowerCase() === projectName.toLowerCase()) {
      parts.pop();
    }

    return parts.length > 0 ? parts.join('/') : 'Root';
  };

  const groupedProjects = useMemo(() => {
    const groups = new Map<string, string[]>();

    for (const p of uniqueProjects) {
      const folder = getProjectFolder(p);
      const list = groups.get(folder) || [];
      list.push(p);
      groups.set(folder, list);
    }

    const sortedFolders = Array.from(groups.keys()).sort((a, b) => {
      if (a === 'Root') return -1;
      if (b === 'Root') return 1;
      return a.localeCompare(b);
    });

    return sortedFolders.map((folder) => ({
      folder,
      projects: (groups.get(folder) || []).sort((a, b) => a.localeCompare(b)),
    }));
  }, [uniqueProjects, projectPaths]);

  const [isStatusMenuOpen, setIsStatusMenuOpen] = useState(false);
  const statusMenuRef = useRef<HTMLDivElement>(null);

  // Close status dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (statusMenuRef.current && !statusMenuRef.current.contains(e.target as Node)) {
        setIsStatusMenuOpen(false);
      }
    };
    if (isStatusMenuOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isStatusMenuOpen]);

  return (
    <header className="toolbar">
      {/* Connection Badge & Actions */}
      <div className="toolbar-brand">
        <div className="status-dropdown-wrapper" ref={statusMenuRef}>
          <button
            type="button"
            className={`status-badge-btn ${connectionStatus} ${isStatusMenuOpen ? 'menu-open' : ''}`}
            onClick={() => setIsStatusMenuOpen((prev) => !prev)}
            title="Connection Status — Click to Rescan or Rebuild Graph"
            aria-haspopup="true"
            aria-expanded={isStatusMenuOpen}
          >
            <span className="status-dot" />
            <span className="status-text">
              {isScanning
                ? `Scanning ${Math.round(scanProgress?.percentage || 0)}%`
                : connectionStatus === 'connected'
                ? 'Connected'
                : connectionStatus === 'connecting'
                ? 'Connecting...'
                : 'Offline'}
            </span>
            <span className="status-chevron">{isStatusMenuOpen ? '▴' : '▾'}</span>
          </button>

          {isStatusMenuOpen && (
            <div className="status-dropdown-menu">
              <div className="status-menu-header">
                <div className="status-menu-title-row">
                  <span className="status-menu-title">CodeExplorer Engine</span>
                  {graphStats?.serverVersion && (
                    <span className="status-menu-version">v{graphStats.serverVersion}</span>
                  )}
                </div>
                {graphStats && graphStats.totalNodes > 0 && (
                  <span className="status-menu-stats">
                    {graphStats.totalNodes.toLocaleString()} nodes · {graphStats.totalEdges.toLocaleString()} relationships
                  </span>
                )}
              </div>
              <div className="status-menu-divider" />
              <button
                type="button"
                className="status-menu-item"
                disabled={connectionStatus !== 'connected' || isScanning}
                onClick={() => {
                  setIsStatusMenuOpen(false);
                  onTriggerScan?.(false);
                }}
                title="Incremental scan: update graph with modified files"
              >
                <span className="menu-item-icon">🔄</span>
                <div className="menu-item-content">
                  <span className="menu-item-label">Rescan Workspace</span>
                  <span className="menu-item-desc">Fast incremental index of changed files</span>
                </div>
              </button>
              <button
                type="button"
                className="status-menu-item danger"
                disabled={connectionStatus !== 'connected' || isScanning}
                onClick={() => {
                  setIsStatusMenuOpen(false);
                  if (window.confirm('Clear graph database and re-index the entire workspace from scratch?')) {
                    onTriggerScan?.(true);
                  }
                }}
                title="Full Re-index: Clears the graph database and re-indexes everything from scratch"
              >
                <span className="menu-item-icon">🧹</span>
                <div className="menu-item-content">
                  <span className="menu-item-label">Rebuild Graph</span>
                  <span className="menu-item-desc">Clear database & full re-index from scratch</span>
                </div>
              </button>
            </div>
          )}
        </div>

        {graphStats && graphStats.totalNodes > 0 && (
          <span
            className="graph-stats-badge"
            title={`${graphStats.totalNodes.toLocaleString()} nodes, ${graphStats.totalEdges.toLocaleString()} relationships`}
          >
            📊 {graphStats.totalNodes >= 1000 ? `${(graphStats.totalNodes / 1000).toFixed(1)}k` : graphStats.totalNodes}
            <span className="stats-label-suffix"> nodes</span>
          </span>
        )}
      </div>

      {/* History Navigation (Undo / Redo) */}
      <div className="history-nav-group">
        <button
          className="history-nav-btn"
          disabled={!canGoBack}
          onClick={onGoBack}
          title={
            canGoBack
              ? `Undo: ${undoDescription || 'action'} (Ctrl+Z / Alt+Left)`
              : 'Nothing to undo (Alt+Left)'
          }
        >
          ◀
        </button>
        <button
          className="history-nav-btn forward"
          disabled={!canGoForward}
          onClick={onGoForward}
          title={
            canGoForward
              ? `Redo: ${redoDescription || 'action'} (Ctrl+Y / Alt+Right)`
              : 'Nothing to redo (Alt+Right)'
          }
        >
          ▶
        </button>
      </div>

      {/* View Mode Dropdown */}
      <div className="view-mode-dropdown-wrap">
        <label className="view-mode-dropdown-label" title="Switch View Mode">
          <span className="view-label-prefix">View:</span>
          <select
            className="view-mode-select"
            value={viewMode}
            onChange={(e) => onViewModeChange(e.target.value as 'semantic' | 'flow' | 'layers' | 'full')}
          >
            <option value="layers">🏛️ System Layers</option>
            <option value="flow">🔀 Project Flow</option>
            <option value="semantic">🧠 Domain Microservice Map</option>
            <option value="full">🌐 Physical Graph</option>
          </select>
        </label>
      </div>

      {/* Dynamic Controls based on Mode */}
      <div className="toolbar-controls">
        {viewMode === 'semantic' && (
          <div className="semantic-controls button-group">
            <button onClick={onRefresh} title="Reload semantic architecture" className="ctrl-btn icon-btn">
              <span className="btn-icon">🔄</span>
              <span className="btn-text">Refresh</span>
            </button>
          </div>
        )}

        {viewMode === 'layers' && (
          <div className="layers-controls button-group">
            <button
              className={`test-toggle-btn ctrl-btn icon-btn ${showTests ? 'active' : ''}`}
              onClick={onToggleShowTests}
              title={showTests ? 'Hide test projects' : 'Show test projects'}
            >
              <span className="btn-icon">🧪</span>
              <span className="btn-text">{showTests ? 'Hide Tests' : 'Show Tests'}</span>
            </button>
            <button onClick={onRefresh} title="Reload layers" className="ctrl-btn icon-btn">
              <span className="btn-icon">🔄</span>
              <span className="btn-text">Refresh</span>
            </button>
          </div>
        )}

        {viewMode === 'flow' && (
          <div className="flow-controls">
            <label className="project-dropdown-label" title="Target Project for Flow Analysis">
              <span className="label-text">Target:</span>
              <select
                className="project-dropdown-select"
                value={selectedProject}
                onChange={(e) => onSelectProject(e.target.value)}
              >
                {uniqueProjects.length === 0 && <option value="">Loading projects...</option>}
                {groupedProjects.length <= 1 && (groupedProjects[0]?.folder === 'Root' || groupedProjects[0]?.folder === 'Projects') ? (
                  groupedProjects[0]?.projects.map((p) => (
                    <option key={p} value={p}>
                      {p}
                    </option>
                  ))
                ) : (
                  groupedProjects.map((group) => (
                    <optgroup key={group.folder} label={`📁 ${group.folder}`}>
                      {group.projects.map((p) => (
                        <option key={p} value={p}>
                          {p}
                        </option>
                      ))}
                    </optgroup>
                  ))
                )}
              </select>
            </label>

            <div className="button-group">
              <button onClick={onRefresh} title="Reload project dependencies" className="ctrl-btn icon-btn">
                <span className="btn-icon">🔄</span>
                <span className="btn-text">Refresh</span>
              </button>
            </div>
          </div>
        )}

        {viewMode === 'full' && (
          <div className="search-box">
            <button
              className={`group-layers-btn ctrl-btn icon-btn ${groupLayers ? 'active' : ''}`}
              onClick={onToggleGroupLayers}
              title={groupLayers ? 'Disable layer grouping (show flat graph)' : 'Group projects into system layers'}
            >
              <span className="btn-icon">📁</span>
              <span className="btn-text">{groupLayers ? 'Ungroup' : 'Group'}</span>
            </button>
            <input
              type="text"
              placeholder="Search or Cypher: MATCH (n)..."
              spellCheck={false}
              value={cypherQuery}
              onChange={(e) => onCypherQueryChange(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && onRunCypher()}
            />
            <button onClick={onRunCypher} title="Execute query" className="ctrl-btn run-btn">
              Run
            </button>
            <div className="button-group">
              <button onClick={onRefresh} title="Reload full architecture" className="ctrl-btn icon-btn">
                <span className="btn-icon">🔄</span>
                <span className="btn-text">Refresh</span>
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Live Scan Progress (active during indexing) */}
      {isScanning && (
        <div className="graph-manage-group">
          <div className="scan-progress-container" title={scanProgress?.currentFile || 'Scanning workspace...'}>
            <div className="scan-progress-header">
              <span>
                <span className="scan-spinner">🔄</span>
                <span className="scan-phase-text">{scanProgress?.phase || 'Scanning'}</span>
              </span>
              <span>{Math.round(scanProgress?.percentage || 0)}%</span>
            </div>
            <div className="scan-progress-bar-bg">
              <div
                className="scan-progress-bar-fill"
                style={{ width: `${Math.max(5, Math.min(100, scanProgress?.percentage || 0))}%` }}
              />
            </div>
          </div>
        </div>
      )}
    </header>
  );
};
