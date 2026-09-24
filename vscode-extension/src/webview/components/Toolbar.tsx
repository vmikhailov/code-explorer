import React, { useMemo } from 'react';
import { ViewMode } from '../commands';

export interface ToolbarProps {
  viewMode: ViewMode;
  onViewModeChange?: (mode: ViewMode) => void;
  allProjects: string[];
  projectPaths?: Record<string, string>;
  selectedProject: string;
  onSelectProject: (project: string) => void;
  connectionStatus?: 'connecting' | 'connected' | 'reconnecting' | 'disconnected' | 'error';
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

  return (
    <header className="toolbar">
      {/* History Navigation (Back / Forward) */}
      <div className="history-nav-group">
        <button
          className="history-nav-btn"
          disabled={!canGoBack}
          onClick={onGoBack}
          title={
            canGoBack
              ? `Back: ${undoDescription || 'action'} (Ctrl+Z / Alt+Left)`
              : 'Nothing to go back (Alt+Left)'
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
              ? `Forward: ${redoDescription || 'action'} (Ctrl+Y / Alt+Right)`
              : 'Nothing to go forward (Alt+Right)'
          }
        >
          ▶
        </button>
      </div>

      {/* View Mode Selector */}
      <div className="view-mode-dropdown-wrap">
        <label className="view-mode-dropdown-label">
          <span className="view-label-prefix">View:</span>
          <select
            className="view-mode-select"
            value={viewMode}
            onChange={(e) => onViewModeChange?.(e.target.value as ViewMode)}
            title="Switch architecture visualization view"
          >
            <option value="semantic">🌐 Domain Microservices</option>
            <option value="layers">🏛️ Architecture Tiers</option>
            <option value="c1">🌍 C1 System Context</option>
            <option value="flow">🔀 Project Flow (C2)</option>
            <option value="full">🕸️ Physical Dependency Graph</option>
            <option value="mermaid">📊 Mermaid Architecture</option>
          </select>
        </label>
      </div>

      {/* Dynamic Controls based on Mode */}
      <div className="toolbar-controls">
        {viewMode === 'c1' && (
          <div className="c1-controls button-group">
            <button onClick={onRefresh} title="Reload C1 system context" className="ctrl-btn icon-btn">
              <span className="btn-icon">🔄</span>
              <span className="btn-text">Refresh</span>
            </button>
          </div>
        )}

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
            <button onClick={onRefresh} title="Reload Architecture Tiers" className="ctrl-btn icon-btn">
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
