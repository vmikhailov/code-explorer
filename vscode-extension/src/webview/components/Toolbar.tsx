import React from 'react';

export interface ToolbarProps {
  viewMode: 'flow' | 'layers' | 'full';
  onViewModeChange: (mode: 'flow' | 'layers' | 'full') => void;
  allProjects: string[];
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
}

export const Toolbar: React.FC<ToolbarProps> = ({
  viewMode,
  onViewModeChange,
  allProjects,
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
}) => {
  return (
    <header className="toolbar">
      {/* Brand & Connection Badge */}
      <div className="toolbar-brand">
        <span className="brand-icon">⚡</span>
        <span className="brand-title">CodeExplorer</span>
        <span className={`status-badge ${connectionStatus}`}>
          {connectionStatus === 'connected' ? 'Connected' : connectionStatus === 'connecting' ? 'Connecting...' : 'Offline'}
        </span>
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
          title="Full solution architecture graph (Cytoscape)"
        >
          🌐 Full Architecture
        </button>
      </div>

      {/* Dynamic Controls based on Mode */}
      <div className="toolbar-controls">
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
                {allProjects.length === 0 && <option value="">Loading projects...</option>}
                {allProjects.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
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
    </header>
  );
};
