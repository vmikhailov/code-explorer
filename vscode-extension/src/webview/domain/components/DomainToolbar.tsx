import React, { useState, useRef, useEffect } from 'react';
import { DomainLayoutName, EdgeCurveMode, EntityKind, PresentationMode } from '../types';
import { useDomainArchitecture } from '../context/DomainArchitectureContext';

interface DomainToolbarProps {
  onZoomIn?: () => void;
  onZoomOut?: () => void;
  onResetZoom?: () => void;
  onFitView?: () => void;
  totalNodesCount: number;
  visibleNodesCount: number;
}

export const DomainToolbar: React.FC<DomainToolbarProps> = ({
  onZoomIn,
  onZoomOut,
  onResetZoom,
  onFitView,
  totalNodesCount,
  visibleNodesCount,
}) => {
  const {
    presentationMode,
    setPresentationMode,
    layoutName,
    setLayoutName,
    edgeCurveMode,
    setEdgeCurveMode,
    searchQuery,
    setSearchQuery,
    hiddenTypes,
    toggleTypeVisibility,
    hideSingleConnectionDbs,
    setHideSingleConnectionDbs,
    hideIsolatedNodes,
    setHideIsolatedNodes,
    resetAllFilters,
    fontSize,
    setFontSize,
    spacingFactor,
    setSpacingFactor,
    onSwitchToContexts,
  } = useDomainArchitecture();

  const [isDisplayMenuOpen, setIsDisplayMenuOpen] = useState(false);
  const displayMenuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (displayMenuRef.current && !displayMenuRef.current.contains(e.target as Node)) {
        setIsDisplayMenuOpen(false);
      }
    };
    if (isDisplayMenuOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isDisplayMenuOpen]);

  const hasActiveFilters =
    searchQuery.trim().length > 0 ||
    hiddenTypes.size > 0 ||
    hideSingleConnectionDbs ||
    hideIsolatedNodes;

  return (
    <div
      className="domain-map-toolbar"
      style={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '8px 16px',
        background: 'var(--vscode-editor-background, #1e1e1e)',
        borderBottom: '1px solid var(--vscode-widget-border, #333)',
        gap: 12,
        flexWrap: 'wrap',
      }}
    >
      {/* Left: View Mode Switcher + Layout Selector */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        {/* Presentation Switcher: Graph | Grid | Matrix */}
        <div
          style={{
            display: 'inline-flex',
            borderRadius: 6,
            background: 'rgba(255, 255, 255, 0.06)',
            padding: 2,
            border: '1px solid var(--vscode-widget-border, #333)',
          }}
        >
          <button
            type="button"
            onClick={() => setPresentationMode('graph')}
            style={{
              padding: '4px 10px',
              fontSize: 12,
              fontWeight: 500,
              borderRadius: 4,
              border: 'none',
              cursor: 'pointer',
              background: presentationMode === 'graph' ? 'var(--vscode-button-background, #0e639c)' : 'transparent',
              color: presentationMode === 'graph' ? 'var(--vscode-button-foreground, #fff)' : 'var(--vscode-foreground, #ccc)',
            }}
            title="Interactive Graph Canvas"
          >
            🕸️ Graph
          </button>
          <button
            type="button"
            onClick={() => setPresentationMode('grid')}
            style={{
              padding: '4px 10px',
              fontSize: 12,
              fontWeight: 500,
              borderRadius: 4,
              border: 'none',
              cursor: 'pointer',
              background: presentationMode === 'grid' ? 'var(--vscode-button-background, #0e639c)' : 'transparent',
              color: presentationMode === 'grid' ? 'var(--vscode-button-foreground, #fff)' : 'var(--vscode-foreground, #ccc)',
            }}
            title="Unified Domain Grid"
          >
            ⊞ Grid
          </button>
          <button
            type="button"
            onClick={() => setPresentationMode('matrix')}
            style={{
              padding: '4px 10px',
              fontSize: 12,
              fontWeight: 500,
              borderRadius: 4,
              border: 'none',
              cursor: 'pointer',
              background: presentationMode === 'matrix' ? 'var(--vscode-button-background, #0e639c)' : 'transparent',
              color: presentationMode === 'matrix' ? 'var(--vscode-button-foreground, #fff)' : 'var(--vscode-foreground, #ccc)',
            }}
            title="Interaction Matrix"
          >
            ▦ Matrix
          </button>
        </div>

        {/* Graph Layout Selector (when in graph mode) */}
        {presentationMode === 'graph' && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
            <select
              value={layoutName}
              onChange={(e) => setLayoutName(e.target.value as DomainLayoutName)}
              style={{
                background: 'var(--vscode-dropdown-background, #252526)',
                color: 'var(--vscode-dropdown-foreground, #ccc)',
                border: '1px solid var(--vscode-dropdown-border, #3c3c3c)',
                borderRadius: 4,
                padding: '4px 8px',
                fontSize: 12,
                cursor: 'pointer',
              }}
              title="Graph Layout Strategy"
            >
              <option value="concentric">Concentric Orbits</option>
              <option value="concentric-equispaced">Equispaced Orbits</option>
              <option value="concentric-polar-force">Polar-Force Orbits</option>
              <option value="concentric-sectors">Domain Sectors</option>
              <option value="swimlanes">Tier Swimlanes</option>
              <option value="clusters">Domain Islands</option>
              <option value="hive">Hive Plot</option>
              <option value="cose">Force-Directed (CoSE)</option>
            </select>

            <select
              value={edgeCurveMode}
              onChange={(e) => setEdgeCurveMode(e.target.value as EdgeCurveMode)}
              style={{
                background: 'var(--vscode-dropdown-background, #252526)',
                color: 'var(--vscode-dropdown-foreground, #ccc)',
                border: '1px solid var(--vscode-dropdown-border, #3c3c3c)',
                borderRadius: 4,
                padding: '4px 8px',
                fontSize: 12,
                cursor: 'pointer',
              }}
              title="Edge Curve Rendering Mode"
            >
              <option value="avoid-inner">Avoid Center Crossings</option>
              <option value="bezier">Smooth Bezier</option>
              <option value="straight">Straight Lines</option>
            </select>
          </div>
        )}
      </div>

      {/* Center: Search input */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, flex: 1, maxWidth: 320 }}>
        <input
          type="text"
          placeholder="Filter services, domains, databases..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          style={{
            width: '100%',
            padding: '4px 10px',
            fontSize: 12,
            borderRadius: 4,
            background: 'var(--vscode-input-background, #2d2d2d)',
            color: 'var(--vscode-input-foreground, #eee)',
            border: '1px solid var(--vscode-input-border, #3c3c3c)',
            outline: 'none',
          }}
        />
        {searchQuery && (
          <button
            type="button"
            onClick={() => setSearchQuery('')}
            style={{
              background: 'transparent',
              border: 'none',
              color: 'var(--vscode-descriptionForeground, #888)',
              cursor: 'pointer',
              padding: '2px 6px',
            }}
          >
            ✕
          </button>
        )}
      </div>

      {/* Right: Quick toggles + Display menu + Reset */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        {/* Entity type pills */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
          {(['Database', 'Topic', 'Worker', 'ExternalService'] as EntityKind[]).map((kind) => {
            const isHidden = hiddenTypes.has(kind);
            const label =
              kind === 'Database'
                ? '🗄️ DB'
                : kind === 'Topic'
                ? '📨 Msg'
                : kind === 'Worker'
                ? '⚙️ Worker'
                : '🌐 Ext';
            return (
              <button
                key={kind}
                type="button"
                onClick={() => toggleTypeVisibility(kind)}
                style={{
                  padding: '3px 8px',
                  fontSize: 11,
                  borderRadius: 4,
                  border: '1px solid var(--vscode-widget-border, #333)',
                  cursor: 'pointer',
                  background: isHidden ? 'rgba(255, 255, 255, 0.03)' : 'rgba(255, 255, 255, 0.1)',
                  color: isHidden ? 'var(--vscode-disabledForeground, #666)' : 'var(--vscode-foreground, #eee)',
                  opacity: isHidden ? 0.6 : 1,
                  textDecoration: isHidden ? 'line-through' : 'none',
                }}
                title={isHidden ? `Show ${kind}s` : `Hide ${kind}s`}
              >
                {label}
              </button>
            );
          })}
        </div>

        {/* Display Dropdown Menu */}
        <div ref={displayMenuRef} style={{ position: 'relative' }}>
          <button
            type="button"
            onClick={() => setIsDisplayMenuOpen((v) => !v)}
            style={{
              padding: '4px 8px',
              fontSize: 12,
              borderRadius: 4,
              border: '1px solid var(--vscode-widget-border, #333)',
              background: 'rgba(255, 255, 255, 0.05)',
              color: 'var(--vscode-foreground, #ccc)',
              cursor: 'pointer',
            }}
            title="Display Options"
          >
            ⚙️ View Options
          </button>

          {isDisplayMenuOpen && (
            <div
              style={{
                position: 'absolute',
                top: '100%',
                right: 0,
                marginTop: 4,
                width: 220,
                background: 'var(--vscode-menu-background, #252526)',
                border: '1px solid var(--vscode-menu-border, #454545)',
                borderRadius: 6,
                boxShadow: '0 6px 16px rgba(0, 0, 0, 0.4)',
                padding: '10px 12px',
                zIndex: 1000,
                display: 'flex',
                flexDirection: 'column',
                gap: 10,
              }}
            >
              <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={hideSingleConnectionDbs}
                  onChange={(e) => setHideSingleConnectionDbs(e.target.checked)}
                />
                <span>Hide Single-Connection DBs</span>
              </label>

              <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={hideIsolatedNodes}
                  onChange={(e) => setHideIsolatedNodes(e.target.checked)}
                />
                <span>Hide Isolated Nodes</span>
              </label>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: '#888' }}>
                  <span>Font Size</span>
                  <span>{fontSize}px</span>
                </div>
                <input
                  type="range"
                  min="9"
                  max="16"
                  value={fontSize}
                  onChange={(e) => setFontSize(Number(e.target.value))}
                />
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 11, color: '#888' }}>
                  <span>Orbit Spacing</span>
                  <span>{spacingFactor.toFixed(1)}x</span>
                </div>
                <input
                  type="range"
                  min="0.6"
                  max="2.0"
                  step="0.1"
                  value={spacingFactor}
                  onChange={(e) => setSpacingFactor(Number(e.target.value))}
                />
              </div>
            </div>
          )}
        </div>

        {/* Zoom Controls (Graph mode only) */}
        {presentationMode === 'graph' && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            <button
              type="button"
              onClick={onZoomIn}
              style={{
                padding: '4px 8px',
                fontSize: 12,
                borderRadius: 4,
                border: '1px solid var(--vscode-widget-border, #333)',
                background: 'transparent',
                color: 'var(--vscode-foreground, #ccc)',
                cursor: 'pointer',
              }}
              title="Zoom In"
            >
              +
            </button>
            <button
              type="button"
              onClick={onZoomOut}
              style={{
                padding: '4px 8px',
                fontSize: 12,
                borderRadius: 4,
                border: '1px solid var(--vscode-widget-border, #333)',
                background: 'transparent',
                color: 'var(--vscode-foreground, #ccc)',
                cursor: 'pointer',
              }}
              title="Zoom Out"
            >
              -
            </button>
            <button
              type="button"
              onClick={onFitView}
              style={{
                padding: '4px 8px',
                fontSize: 12,
                borderRadius: 4,
                border: '1px solid var(--vscode-widget-border, #333)',
                background: 'transparent',
                color: 'var(--vscode-foreground, #ccc)',
                cursor: 'pointer',
              }}
              title="Fit View"
            >
              Fit
            </button>
          </div>
        )}

        {/* Reset All Filters Button */}
        {hasActiveFilters && (
          <button
            type="button"
            onClick={resetAllFilters}
            style={{
              padding: '4px 8px',
              fontSize: 11,
              borderRadius: 4,
              border: 'none',
              background: 'rgba(239, 68, 68, 0.2)',
              color: '#f87171',
              cursor: 'pointer',
            }}
            title="Reset All Active Filters"
          >
            Reset Filters
          </button>
        )}

        {/* Switch to Context Map */}
        {onSwitchToContexts && (
          <button
            type="button"
            onClick={onSwitchToContexts}
            style={{
              padding: '4px 10px',
              fontSize: 12,
              borderRadius: 4,
              border: '1px solid rgba(56, 189, 248, 0.3)',
              background: 'rgba(56, 189, 248, 0.1)',
              color: '#38bdf8',
              cursor: 'pointer',
            }}
            title="Switch to Strategic Bounded Context Map"
          >
            🗺️ Contexts
          </button>
        )}
      </div>
    </div>
  );
};
