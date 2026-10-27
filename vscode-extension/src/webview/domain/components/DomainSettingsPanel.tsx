import React, { useState } from 'react';
import {
  DomainLayoutName,
  EdgeCurveMode,
} from '../../components/DomainArchitectureView';
import { NodeContentMode, NodeSizingMode } from '../types';
import {
  SettingsIcon,
  ChevronDownIcon,
  ChevronRightIcon,
} from '../../components/Icons';

export interface ProjectMapPresetsState {
  cyclesCount: number;
  cyclesOnly: boolean;
  hideUtilities: boolean;
  hideResources: boolean;
  projectName?: string;
  filesCount?: number;
  entryPointCount?: number;
  moduleCount?: number;
  granularity?: 'class' | 'file' | 'module';
  depCount?: number;
  resourceCount?: number;
}

export interface DomainSettingsPanelProps {
  layoutName: DomainLayoutName;
  onSetLayout?: (layoutName: DomainLayoutName) => void;
  spacingFactor: number;
  orbitDistance: number;
  minOrbitNodeDistance: number;
  autoRelayoutOnFilter: boolean;
  onSetSpacing: (factor: number) => void;
  onSetOrbitDistance: (dist: number) => void;
  onSetMinOrbitNodeDistance: (dist: number) => void;
  onSetAutoRelayout: (enabled: boolean) => void;
  onForceRelayout: () => void;

  nodeContentMode: NodeContentMode;
  nodeSizingMode: NodeSizingMode;
  edgeCurveMode: EdgeCurveMode;
  edgeLabelsOnHover: boolean;
  baseIconSize: number;
  fontSize: number;
  curveFactor: number;
  wheelSensitivity: number;

  onSetNodeContentMode: (mode: NodeContentMode) => void;
  onSetNodeSizingMode: (mode: NodeSizingMode) => void;
  onSetEdgeCurveMode: (mode: EdgeCurveMode) => void;
  onSetEdgeLabelsOnHover: (onHover: boolean) => void;
  onSetBaseIconSize: (size: number) => void;
  onSetFontSize: (size: number) => void;
  onSetCurveFactor: (factor: number) => void;
  onSetWheelSensitivity: (sens: number) => void;

  isMatrixMode?: boolean;
}

export const DomainSettingsPanel: React.FC<DomainSettingsPanelProps> = ({
  layoutName,
  onSetLayout,
  spacingFactor,
  orbitDistance,
  minOrbitNodeDistance,
  autoRelayoutOnFilter,
  onSetSpacing,
  onSetOrbitDistance,
  onSetMinOrbitNodeDistance,
  onSetAutoRelayout,
  onForceRelayout,
  nodeContentMode,
  nodeSizingMode,
  edgeCurveMode,
  edgeLabelsOnHover,
  baseIconSize,
  fontSize,
  curveFactor,
  wheelSensitivity,
  onSetNodeContentMode,
  onSetNodeSizingMode,
  onSetEdgeCurveMode,
  onSetEdgeLabelsOnHover,
  onSetBaseIconSize,
  onSetFontSize,
  onSetCurveFactor,
  onSetWheelSensitivity,
  isMatrixMode = false,
}) => {
  const [collapsedSections, setCollapsedSections] = useState<Record<string, boolean>>({});

  const toggleSection = (id: string) => {
    setCollapsedSections((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const isConcentric = !layoutName || layoutName.startsWith('concentric');

  if (isMatrixMode) {
    return (
      <div style={{ padding: '16px', textAlign: 'center', color: '#64748b', fontSize: '11px' }}>
        Settings are not available in Architecture Matrix view.
      </div>
    );
  }

  return (
    <div
      className="domain-settings-panel"
      style={{
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        minHeight: 0,
        overflowY: 'auto',
        background: 'var(--vscode-sideBar-background, #181818)',
        padding: '10px 12px',
        gap: '12px',
      }}
    >
      {/* 1. Layout & Geometry Settings (formerly under Orbits Gear) */}
      <div className="visibility-section">
        <div
          className="visibility-section-header"
          style={{ cursor: 'pointer', userSelect: 'none' }}
          onClick={() => toggleSection('geometry')}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            {collapsedSections['geometry'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
            <span className="visibility-section-title">Layout & Spacing</span>
          </div>
          <span className="visibility-section-hint">Air, orbit distance, auto-recalc</span>
        </div>

        {!collapsedSections['geometry'] && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', paddingTop: '4px' }}>
            {/* Graph Layout Selector */}
            {onSetLayout && (
              <div className="display-setting-row" title="Select graph layout algorithm">
                <div className="setting-label-row">
                  <span className="setting-icon">📐</span>
                  <span className="setting-name">Graph Layout</span>
                </div>
                <select
                  value={layoutName}
                  onChange={(e) => onSetLayout(e.target.value as DomainLayoutName)}
                  title="Graph Layout Algorithm"
                  className="domain-layout-dropdown"
                  style={{ width: '100%', marginTop: '3px' }}
                >
                  <option value="concentric">🎯 Concentric (Original)</option>
                  <option value="concentric-equispaced">🪐 Concentric: Equispaced</option>
                  <option value="concentric-polar-force">🪐 Concentric: Polar Force</option>
                  <option value="concentric-sectors">🪐 Concentric: Domain Sectors</option>
                  <option value="swimlanes">🏊 Swimlanes (Pipeline)</option>
                  <option value="mermaid">🧜 Mermaid</option>
                  <option value="dagre">📊 Dagre Flow</option>
                  <option value="clusters">🏝️ Domain Islands (Bounded Contexts)</option>
                  <option value="hive">🕸️ Hive Plot (Multi-Axis)</option>
                  <option value="cose">⚡ Force (COSE)</option>
                </select>
              </div>
            )}

            {/* Node Spacing / Air Control */}
            <div className="display-setting-row" title="Adjust node spacing / distance between orbits">
              <div className="setting-label-row">
                <span className="setting-icon">💨</span>
                <span className="setting-name">Node Air (Spacing)</span>
                <span
                  className="domain-hud-value-badge"
                  onClick={() => onSetSpacing(1.0)}
                  title="Click to reset air to 1.0x"
                >
                  {spacingFactor.toFixed(1)}x
                </span>
              </div>
              <div className="setting-slider-row">
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => onSetSpacing(Math.max(0.4, +(spacingFactor - 0.2).toFixed(1)))}
                  title="Decrease spacing"
                >
                  −
                </button>
                <input
                  type="range"
                  min="0.5"
                  max="3.0"
                  step="0.1"
                  value={spacingFactor}
                  onChange={(e) => onSetSpacing(parseFloat(e.target.value))}
                  className="domain-hud-slider"
                />
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => onSetSpacing(Math.min(3.5, +(spacingFactor + 0.2).toFixed(1)))}
                  title="Increase spacing"
                >
                  +
                </button>
              </div>
            </div>

            {/* Orbit Distance Control (Concentric only) */}
            {isConcentric && (
              <div className="display-setting-row" title="Adjust distance between concentric orbits (100px - 900px)">
                <div className="setting-label-row">
                  <span className="setting-icon">🪐</span>
                  <span className="setting-name">Orbit Distance</span>
                  <span
                    className="domain-hud-value-badge"
                    onClick={() => onSetOrbitDistance(280)}
                    title="Click to reset orbit distance to 280px"
                  >
                    {orbitDistance}px
                  </span>
                </div>
                <div className="setting-slider-row">
                  <button
                    type="button"
                    className="domain-hud-step-btn"
                    onClick={() => onSetOrbitDistance(orbitDistance - 20)}
                    title="Decrease orbit distance (-20px)"
                  >
                    −
                  </button>
                  <input
                    type="range"
                    min="100"
                    max="900"
                    step="10"
                    value={orbitDistance}
                    onChange={(e) => onSetOrbitDistance(parseInt(e.target.value, 10))}
                    className="domain-hud-slider"
                  />
                  <button
                    type="button"
                    className="domain-hud-step-btn"
                    onClick={() => onSetOrbitDistance(orbitDistance + 20)}
                    title="Increase orbit distance (+20px)"
                  >
                    +
                  </button>
                </div>
              </div>
            )}

            {/* Min Distance on Orbit Control (Concentric only) */}
            {isConcentric && (
              <div
                className="display-setting-row"
                title="Adjust minimum distance between nodes on the same orbit (50px - 400px)"
              >
                <div className="setting-label-row">
                  <span className="setting-icon">↔️</span>
                  <span className="setting-name">Min Distance on Orbit</span>
                  <span
                    className="domain-hud-value-badge"
                    onClick={() => onSetMinOrbitNodeDistance(125)}
                    title="Click to reset min orbit node distance to 125px"
                  >
                    {minOrbitNodeDistance}px
                  </span>
                </div>
                <div className="setting-slider-row">
                  <button
                    type="button"
                    className="domain-hud-step-btn"
                    onClick={() => onSetMinOrbitNodeDistance(minOrbitNodeDistance - 15)}
                    title="Decrease min distance on orbit (-15px)"
                  >
                    −
                  </button>
                  <input
                    type="range"
                    min="50"
                    max="400"
                    step="5"
                    value={minOrbitNodeDistance}
                    onChange={(e) => onSetMinOrbitNodeDistance(parseInt(e.target.value, 10))}
                    className="domain-hud-slider"
                  />
                  <button
                    type="button"
                    className="domain-hud-step-btn"
                    onClick={() => onSetMinOrbitNodeDistance(minOrbitNodeDistance + 15)}
                    title="Increase min distance on orbit (+15px)"
                  >
                    +
                  </button>
                </div>
              </div>
            )}

            {/* Auto-recalc toggle & Re-layout button */}
            <div
              className="display-popover-actions"
              style={{
                paddingTop: '4px',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
              }}
            >
              <label
                className={`domain-hud-checkbox-label ${autoRelayoutOnFilter ? 'is-active' : ''}`}
                title={
                  autoRelayoutOnFilter
                    ? 'Auto-relayout enabled: layout automatically refits when entities are hidden'
                    : 'Auto-relayout disabled: nodes are hidden in place without moving remaining nodes'
                }
              >
                <input
                  type="checkbox"
                  className="domain-hud-checkbox"
                  checked={autoRelayoutOnFilter}
                  onChange={(e) => onSetAutoRelayout(e.target.checked)}
                />
                <span>🔄 Auto-recalc layout</span>
              </label>
              <button
                type="button"
                className="domain-hud-btn"
                onClick={onForceRelayout}
                title="Recalculate layout immediately"
                style={{ padding: '2px 8px', fontSize: '11px', lineHeight: 1 }}
              >
                ⟳ Re-layout
              </button>
            </div>
          </div>
        )}
      </div>

      {/* 2. Node & Edge Appearance Settings (formerly under Visibility Gear) */}
      <div className="visibility-section">
        <div
          className="visibility-section-header"
          style={{ cursor: 'pointer', userSelect: 'none' }}
          onClick={() => toggleSection('appearance')}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            {collapsedSections['appearance'] ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
            <span className="visibility-section-title">Appearance & Styling</span>
          </div>
          <span className="visibility-section-hint">Node size, line curves, labels</span>
        </div>

        {!collapsedSections['appearance'] && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', paddingTop: '4px' }}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
              {/* Node Content Mode */}
              <div className="display-setting-row" title="Choose what is displayed inside each node shape">
                <div className="setting-label-row">
                  <span className="setting-icon">🎨</span>
                  <span className="setting-name">Node Content</span>
                </div>
                <select
                  value={nodeContentMode}
                  onChange={(e) => onSetNodeContentMode(e.target.value as NodeContentMode)}
                  title="Node Content: Clean Icon, Icon + 3-letter Code, or Metric"
                  className="domain-layout-dropdown"
                  style={{ width: '100%', marginTop: '3px' }}
                >
                  <option value="icon">✨ Icon Only</option>
                  <option value="icon-code">🏷️ Icon + Code</option>
                  <option value="metric">📊 Icon + Metric</option>
                </select>
              </div>

              {/* Node Sizing Mode */}
              <div className="display-setting-row" title="Scale node size uniformly, by √Weight, or by degree">
                <div className="setting-label-row">
                  <span className="setting-icon">📏</span>
                  <span className="setting-name">Node Size</span>
                </div>
                <select
                  value={nodeSizingMode}
                  onChange={(e) => onSetNodeSizingMode(e.target.value as NodeSizingMode)}
                  title="Node Size: Uniform, √Weight (Files/Projects), or Connections (Degree)"
                  className="domain-layout-dropdown"
                  style={{ width: '100%', marginTop: '3px' }}
                >
                  <option value="uniform">⚪ Uniform</option>
                  <option value="sqrt-weight">📐 √Weight</option>
                  <option value="degree">🔗 Degree</option>
                </select>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
              {/* Edge Curve Mode */}
              <div className="display-setting-row" title="Line Style: Straight, Bezier curve, or Bypass Inner Orbits">
                <div className="setting-label-row">
                  <span className="setting-icon">〰️</span>
                  <span className="setting-name">Line Style</span>
                </div>
                <select
                  value={edgeCurveMode}
                  onChange={(e) => onSetEdgeCurveMode(e.target.value as EdgeCurveMode)}
                  title="Line Style: Straight, Bezier curve, or Bypass Inner Orbits"
                  className="domain-layout-dropdown"
                  style={{ width: '100%', marginTop: '3px' }}
                >
                  <option value="bezier">〰️ Bezier</option>
                  <option value="straight">📏 Straight</option>
                  <option value="avoid-inner">🛡️ Bypass Orbits</option>
                </select>
              </div>

              {/* Edge Labels Visibility */}
              <div className="display-setting-row" title="Control when edge text labels are shown: always or only on hover">
                <div className="setting-label-row">
                  <span className="setting-icon">🏷️</span>
                  <span className="setting-name">Edge Labels</span>
                </div>
                <select
                  value={edgeLabelsOnHover ? 'hover' : 'always'}
                  onChange={(e) => onSetEdgeLabelsOnHover(e.target.value === 'hover')}
                  title="Edge Labels visibility"
                  className="domain-layout-dropdown"
                  style={{ width: '100%', marginTop: '3px' }}
                >
                  <option value="always">Always Show</option>
                  <option value="hover">On Hover Only</option>
                </select>
              </div>
            </div>

            {/* Base Icon Size Control */}
            <div className="display-setting-row" title="Adjust baseline node icon size (28px - 84px, default 52px)">
              <div className="setting-label-row">
                <span className="setting-icon">🎨</span>
                <span className="setting-name">Base Icon Size</span>
                <span
                  className="domain-hud-value-badge"
                  onClick={() => onSetBaseIconSize(52)}
                  title="Click to reset base icon size to 52px"
                >
                  {baseIconSize}px
                </span>
              </div>
              <div className="setting-slider-row">
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => onSetBaseIconSize(Math.max(28, baseIconSize - 2))}
                  title="Decrease icon size (-2px)"
                >
                  −
                </button>
                <input
                  type="range"
                  min="28"
                  max="84"
                  step="2"
                  value={baseIconSize}
                  onChange={(e) => onSetBaseIconSize(parseInt(e.target.value, 10))}
                  className="domain-hud-slider"
                />
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => onSetBaseIconSize(Math.min(84, baseIconSize + 2))}
                  title="Increase icon size (+2px)"
                >
                  +
                </button>
              </div>
            </div>

            {/* Font Size Control */}
            <div className="display-setting-row" title="Adjust graph label font size (7px - 24px)">
              <div className="setting-label-row">
                <span className="setting-icon">🔤</span>
                <span className="setting-name">Font Size</span>
                <span
                  className="domain-hud-value-badge"
                  onClick={() => onSetFontSize(10)}
                  title="Click to reset font size to 10px"
                >
                  {fontSize}px
                </span>
              </div>
              <div className="setting-slider-row">
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => onSetFontSize(Math.max(7, fontSize - 1))}
                  title="Decrease font size"
                >
                  −
                </button>
                <input
                  type="range"
                  min="7"
                  max="24"
                  step="1"
                  value={fontSize}
                  onChange={(e) => onSetFontSize(parseInt(e.target.value, 10))}
                  className="domain-hud-slider"
                />
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => onSetFontSize(Math.min(24, fontSize + 1))}
                  title="Increase font size"
                >
                  +
                </button>
              </div>
            </div>

            {/* Line Curvature Control (when bezier) */}
            {edgeCurveMode === 'bezier' && (
              <div className="display-setting-row" title="Adjust edge curve intensity (0 = straight, 100 = full loop)">
                <div className="setting-label-row">
                  <span className="setting-icon">➰</span>
                  <span className="setting-name">Line Curvature</span>
                  <span
                    className="domain-hud-value-badge"
                    onClick={() => onSetCurveFactor(35)}
                    title="Click to reset curve to 35"
                  >
                    {curveFactor}
                  </span>
                </div>
                <div className="setting-slider-row">
                  <button
                    type="button"
                    className="domain-hud-step-btn"
                    onClick={() => onSetCurveFactor(Math.max(0, curveFactor - 5))}
                    title="Decrease curvature"
                  >
                    −
                  </button>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    step="5"
                    value={curveFactor}
                    onChange={(e) => onSetCurveFactor(parseInt(e.target.value, 10))}
                    className="domain-hud-slider"
                  />
                  <button
                    type="button"
                    className="domain-hud-step-btn"
                    onClick={() => onSetCurveFactor(Math.min(100, curveFactor + 5))}
                    title="Increase curvature"
                  >
                    +
                  </button>
                </div>
              </div>
            )}

            {/* Mouse Wheel Sensitivity */}
            <div className="display-setting-row" title="Adjust canvas zoom speed when using mouse wheel / trackpad">
              <div className="setting-label-row">
                <span className="setting-icon">🖱️</span>
                <span className="setting-name">Wheel Zoom Speed</span>
                <span
                  className="domain-hud-value-badge"
                  onClick={() => onSetWheelSensitivity(1.0)}
                  title="Click to reset wheel sensitivity to 1.0x"
                >
                  {wheelSensitivity.toFixed(1)}x
                </span>
              </div>
              <div className="setting-slider-row">
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => onSetWheelSensitivity(Math.max(0.5, +(wheelSensitivity - 0.2).toFixed(1)))}
                  title="Decrease zoom sensitivity"
                >
                  −
                </button>
                <input
                  type="range"
                  min="0.5"
                  max="3.0"
                  step="0.1"
                  value={wheelSensitivity}
                  onChange={(e) => onSetWheelSensitivity(parseFloat(e.target.value))}
                  className="domain-hud-slider"
                />
                <button
                  type="button"
                  className="domain-hud-step-btn"
                  onClick={() => onSetWheelSensitivity(Math.min(3.0, +(wheelSensitivity + 0.2).toFixed(1)))}
                  title="Increase zoom sensitivity"
                >
                  +
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
