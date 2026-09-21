import React, { useMemo } from 'react';

export interface ToolbarProps {
  viewMode: 'semantic' | 'flow' | 'layers' | 'full';
  onViewModeChange: (mode: 'semantic' | 'flow' | 'layers' | 'full') => void;
  allProjects: string[];
  projectPaths?: Record<string, string>;
  selectedProject: string;
  onSelectProject: (project: string) => void;
  connectionStatus: 'connecting' | 'connected' | 'disconnected';
  onFitView: () => void;
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
  groupLayers: boolean;
  onToggleGroupLayers: () => void;
  isScanning?: boolean;
  scanProgress?: { phase: string; percentage: number; currentFile?: string; totalFiles?: number; processedFiles?: number } | null;
  onTriggerScan?: (clear?: boolean) => void;
  graphStats?: { totalNodes: number; totalEdges: number } | null;
}

export const Toolbar: React.FC<ToolbarProps> = ({
  viewMode,
  onViewModeChange,
  allProjects,
  projectPaths,
  selectedProject,
  onSelectProject,
  connectionStatus,
  onFitView,
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

  return (
    <header className="toolbar">
      {/* Brand & Connection Badge */}
      <div className="toolbar-brand">
        <span className="brand-icon">⚡</span>
        <span className="brand-title">CodeExplorer</span>
        <span className={`status-badge ${connectionStatus}`}>
          {connectionStatus === 'connected' ? 'Connected' : connectionStatus === 'connecting' ? 'Connecting...' : 'Offline'}
        </span>
        {graphStats && graphStats.totalNodes > 0 && (
          <span
            className="graph-stats-badge"
            title={`${graphStats.totalNodes.toLocaleString()} nodes, ${graphStats.totalEdges.toLocaleString()} relationships`}
          >
            📊 {graphStats.totalNodes >= 1000 ? `${(graphStats.totalNodes / 1000).toFixed(1)}k` : graphStats.totalNodes} nodes
          </span>
        )}
      </div>

      {/* History Navigation (Back / Forward) */}
      <div className="history-nav-group">
        <button
          className="history-nav-btn"
          disabled={!canGoBack}
          onClick={onGoBack}
          title="Go back (Alt+Left)"
        >
          ◀ Back
        </button>
        <button
          className="history-nav-btn forward"
          disabled={!canGoForward}
          onClick={onGoForward}
          title="Go forward (Alt+Right)"
        >
          ▶
        </button>
      </div>

      {/* Mode Switcher Tabs */}
      <div className="view-mode-tabs">
        <button
          className={`mode-tab-btn ${viewMode === 'semantic' ? 'active' : ''}`}
          onClick={() => onViewModeChange('semantic')}
          title="Semantic architecture graph: Services, Databases, Brokers, and APIs (internal libraries abstracted away)"
        >
          🧠 Semantic Graph
        </button>
        <button
          className={`mode-tab-btn ${viewMode === 'layers' ? 'active' : ''}`}
          onClick={() => onViewModeChange('layers')}
          title="Hierarchical collapsible system layers"
        >
          🏛️ System Layers
        </button>
        <button
          className={`mode-tab-btn ${viewMode === 'flow' ? 'active' : ''}`}
          onClick={() => onViewModeChange('flow')}
          title="Focused 3-column project dependency flow (React Flow)"
        >
          🔀 Project Flow
        </button>
        <button
          className={`mode-tab-btn ${viewMode === 'full' ? 'active' : ''}`}
          onClick={() => onViewModeChange('full')}
          title="Full solution physical architecture graph (Cytoscape)"
        >
          🌐 Physical Graph
        </button>
      </div>

      {/* Dynamic Controls based on Mode */}
      <div className="toolbar-controls">
        {viewMode === 'semantic' && (
          <div className="semantic-controls button-group">
            <button onClick={onFitView} title="Center and fit to screen">
              Fit
            </button>
            <button onClick={onRefresh} title="Reload semantic architecture">
              Refresh
            </button>
          </div>
        )}

        {viewMode === 'layers' && (
          <div className="layers-controls">
            <button
              className={`test-toggle-btn ${showTests ? 'active' : ''}`}
              onClick={onToggleShowTests}
              title={showTests ? 'Hide test projects' : 'Show test projects'}
            >
              🧪 {showTests ? 'Hide Tests' : 'Show Tests'}
            </button>
            <button onClick={onRefresh} title="Reload layers">
              Refresh
            </button>
          </div>
        )}

        {viewMode === 'flow' && (
          <div className="flow-controls">
            <label className="project-dropdown-label">
              <span className="label-text">Target Project:</span>
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
              <button onClick={onFitView} title="Center and fit to screen">
                Fit
              </button>
              <button onClick={onRefresh} title="Reload project dependencies">
                Refresh
              </button>
            </div>
          </div>
        )}

        {viewMode === 'full' && (
          <div className="search-box">
            <button
              className={`group-layers-btn ${groupLayers ? 'active' : ''}`}
              onClick={onToggleGroupLayers}
              title={groupLayers ? 'Disable layer grouping (show flat graph)' : 'Group projects into system layers'}
            >
              📁 {groupLayers ? 'Ungroup' : 'Group Layers'}
            </button>
            <input
              type="text"
              placeholder="Search or run Cypher: MATCH (n)-[r]->(m) RETURN n,r,m LIMIT 50"
              spellCheck={false}
              value={cypherQuery}
              onChange={(e) => onCypherQueryChange(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && onRunCypher()}
            />
            <button onClick={onRunCypher} title="Execute query">
              Run
            </button>
            <div className="button-group">
              <button onClick={onFitView} title="Fit to view">Fit</button>
              <button onClick={onRefresh} title="Reload full architecture">Refresh</button>
            </div>
          </div>
        )}
      </div>

      {/* Graph Management: Rescan / Progress / Full Re-index */}
      <div className="graph-manage-group">
        {isScanning ? (
          <div className="scan-progress-container" title={scanProgress?.currentFile || 'Scanning workspace...'}>
            <div className="scan-progress-header">
              <span>
                <span className="scan-spinner">🔄</span>
                {scanProgress?.phase || 'Scanning'}
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
        ) : (
          <div className="button-group">
            <button
              className="scan-btn"
              onClick={() => onTriggerScan?.(false)}
              disabled={connectionStatus !== 'connected'}
              title="Rescan Workspace: Incremental scan of workspace files"
            >
              🔄 Rescan
            </button>
            <button
              className="scan-btn danger"
              onClick={() => {
                if (window.confirm('Clear graph database and re-index the entire workspace from scratch?')) {
                  onTriggerScan?.(true);
                }
              }}
              disabled={connectionStatus !== 'connected'}
              title="Full Re-index: Clears the graph database and re-indexes everything from scratch"
            >
              🧹 Rebuild
            </button>
          </div>
        )}
      </div>
    </header>
  );
};
