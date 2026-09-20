import React from 'react';

export interface ToolbarProps {
  viewMode: 'flow' | 'full';
  onViewModeChange: (mode: 'flow' | 'full') => void;
  allProjects: string[];
  selectedProject: string;
  onSelectProject: (project: string) => void;
  connectionStatus: 'connecting' | 'connected' | 'disconnected';
  onFitView: () => void;
  onRefresh: () => void;
  cypherQuery: string;
  onCypherQueryChange: (query: string) => void;
  onRunCypher: () => void;
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

      {/* Mode Switcher Tabs */}
      <div className="view-mode-tabs">
        <button
          className={`mode-tab-btn ${viewMode === 'flow' ? 'active' : ''}`}
          onClick={() => onViewModeChange('flow')}
          title="Focused 3-column project dependency flow"
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
        {viewMode === 'flow' ? (
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
        ) : (
          <div className="search-box">
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
