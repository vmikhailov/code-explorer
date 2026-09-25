import React, { useState, useMemo } from 'react';
import { GraphData, GraphNode } from '../../../../proto/types';
import { LayerDefinition, getLayersFromGraph } from '../layers';

export interface LayeredArchitectureViewProps {
  graph: GraphData | null;
  onOpenFile: (filePath: string, lineStart?: number) => void;
  onFocusInFlow: (projectName: string) => void;
  showTests: boolean;
  collapsedLayers?: Set<string>;
  onToggleLayer?: (layerId: string) => void;
  onSelectNode?: (node: GraphNode | null) => void;
  selectedNodeId?: string;
}

export const LayeredArchitectureView: React.FC<LayeredArchitectureViewProps> = ({
  graph,
  onOpenFile,
  onFocusInFlow,
  showTests,
  collapsedLayers: collapsedLayersProp,
  onToggleLayer: onToggleLayerProp,
  onSelectNode,
  selectedNodeId,
}) => {
  const [localCollapsedLayers, setLocalCollapsedLayers] = useState<Set<string>>(new Set());
  const collapsedLayers = collapsedLayersProp !== undefined ? collapsedLayersProp : localCollapsedLayers;

  // Parse layers from metadata or use default
  const layerDefs = useMemo(() => getLayersFromGraph(graph), [graph]);

  // Group nodes by layerId
  const nodesByLayer = useMemo(() => {
    const map = new Map<string, GraphNode[]>();
    for (const def of layerDefs) {
      map.set(def.layerId, []);
    }

    const seenNodeIds = new Set<string>();
    for (const node of graph?.nodes || []) {
      const lowerId = node.id.toLowerCase();
      if (seenNodeIds.has(lowerId) || node.kind === 'Package') continue;
      seenNodeIds.add(lowerId);

      const rawLayerId = node.properties?.layerId || node.properties?.layer || 'layer_components';
      const layerId = map.has(rawLayerId) ? rawLayerId : 'layer_components';
      const list = map.get(layerId) || [];
      list.push(node);
      map.set(layerId, list);
    }

    return map;
  }, [graph, layerDefs]);

  // Dependency count maps for badges
  const { incomingCounts, outgoingCounts } = useMemo(() => {
    const inc: Record<string, number> = {};
    const out: Record<string, number> = {};
    for (const edge of graph?.edges || []) {
      out[edge.source] = (out[edge.source] || 0) + 1;
      inc[edge.target] = (inc[edge.target] || 0) + 1;
    }
    return { incomingCounts: inc, outgoingCounts: out };
  }, [graph]);

  const getProjectLanguageLabel = (node: GraphNode) => {
    if (node.kind === 'Database') return node.properties?.db_type || 'DB';
    if (node.kind === 'ExternalService') return 'Service';
    const pt = (node.properties?.project_type || node.properties?.language || '').toLowerCase();
    if (pt === 'go' || pt === 'golang') return 'Go';
    if (pt === 'csharp' || pt === 'cs' || pt === 'dotnet') return 'C#';
    if (pt === 'typescript' || pt === 'ts') return 'TypeScript';
    if (pt === 'javascript' || pt === 'js') return 'JavaScript';
    if (pt === 'python' || pt === 'py') return 'Python';
    if (pt === 'java') return 'Java';
    if (pt === 'sql') return 'SQL';
    return node.properties?.framework || 'Project';
  };

  const localToggleLayer = (layerId: string) => {
    setLocalCollapsedLayers((prev) => {
      const next = new Set(prev);
      if (next.has(layerId)) {
        next.delete(layerId);
      } else {
        next.add(layerId);
      }
      return next;
    });
  };

  const toggleLayer = onToggleLayerProp || localToggleLayer;

  const collapseAll = () => {
    if (onToggleLayerProp) {
      for (const l of layerDefs) {
        if (!collapsedLayers.has(l.layerId)) {
          onToggleLayerProp(l.layerId);
        }
      }
    } else {
      setLocalCollapsedLayers(new Set(layerDefs.map((l) => l.layerId)));
    }
  };

  const expandAll = () => {
    if (onToggleLayerProp) {
      for (const l of layerDefs) {
        if (collapsedLayers.has(l.layerId)) {
          onToggleLayerProp(l.layerId);
        }
      }
    } else {
      setLocalCollapsedLayers(new Set());
    }
  };

  const visibleLayers = layerDefs.filter((l) => showTests || l.layerId !== 'layer_tests');

  return (
    <div className="layers-view-container">
      {/* Top Quick Actions Bar */}
      <div className="layers-quick-actions">
        <div className="layer-stats-info">
          <span>Architectural Tiers: <strong>{visibleLayers.length}</strong></span>
          <span>•</span>
          <span>Total Projects &amp; DBs: <strong>{graph?.nodes?.length || 0}</strong></span>
        </div>
        <div className="layers-action-buttons">
          <button className="small-action-btn" onClick={expandAll} title="Expand all layers">
            Expand All
          </button>
          <button className="small-action-btn" onClick={collapseAll} title="Collapse all layers into macro bars">
            Collapse All
          </button>
        </div>
      </div>

      {/* Layer Tiers Stack */}
      <div className="layers-stack">
        {visibleLayers.map((layer, index) => {
          const nodes = nodesByLayer.get(layer.layerId) || [];
          const isCollapsed = collapsedLayers.has(layer.layerId);

          if (nodes.length === 0 && layer.layerId === 'layer_tests') {
            return null; // Skip empty tests
          }

          return (
            <React.Fragment key={layer.layerId}>
              {/* Layer Container */}
              <section
                className={`layer-tier-card ${isCollapsed ? 'collapsed' : 'expanded'}`}
                style={{ borderLeftColor: layer.color }}
              >
                {/* Layer Header */}
                <header
                  className="layer-header"
                  onClick={() => toggleLayer(layer.layerId)}
                  title="Click to collapse / expand this layer"
                >
                  <div className="layer-title-area">
                    <span className="layer-icon">{layer.icon}</span>
                    <div className="layer-name-group">
                      <h3 className="layer-name" style={{ color: layer.color }}>
                        {layer.layerName}
                      </h3>
                      <span className="layer-desc">{layer.description}</span>
                    </div>
                  </div>

                  <div className="layer-header-controls">
                    <span className="layer-count-badge" style={{ backgroundColor: `${layer.color}22`, color: layer.color }}>
                      {nodes.length} {nodes.length === 1 ? 'item' : 'items'}
                    </span>
                    <button
                      className="layer-toggle-btn"
                      onClick={(e) => {
                        e.stopPropagation();
                        toggleLayer(layer.layerId);
                      }}
                      title={isCollapsed ? 'Expand Layer' : 'Collapse Layer'}
                    >
                      {isCollapsed ? '➕ Expand' : '➖ Collapse'}
                    </button>
                  </div>
                </header>

                {/* Layer Body */}
                {isCollapsed ? (
                  <div
                    className="layer-collapsed-summary"
                    onClick={() => toggleLayer(layer.layerId)}
                    title="Click to expand"
                  >
                    <span className="collapsed-preview-text">
                      📦 {nodes.length} projects condensed ({nodes.map((n) => n.name).slice(0, 4).join(', ')}
                      {nodes.length > 4 ? ` +${nodes.length - 4} more` : ''})
                    </span>
                    <span className="expand-hint">Click to expand ➔</span>
                  </div>
                ) : (
                  <div className="layer-nodes-grid">
                    {nodes.map((node) => {
                      const isDb = node.kind === 'Database';
                      const inCount = incomingCounts[node.id] || 0;
                      const outCount = outgoingCounts[node.id] || 0;

                      const isSelected = selectedNodeId ? (node.id === selectedNodeId || node.name === selectedNodeId) : false;

                      return (
                        <div
                          key={node.id}
                          className={`tier-project-card ${isSelected ? 'is-selected' : ''}`}
                          onClick={() => onSelectNode?.(node)}
                        >
                          <div className="tier-card-header">
                            <span
                              className="tier-node-kind"
                              style={{ color: layer.color, backgroundColor: `${layer.color}15` }}
                            >
                              {getProjectLanguageLabel(node)}
                            </span>
                            <div className="tier-io-badges">
                              {inCount > 0 && (
                                <span className="io-badge in" title={`${inCount} incoming callers`}>
                                  ← {inCount}
                                </span>
                              )}
                              {outCount > 0 && (
                                <span className="io-badge out" title={`Depends on ${outCount} items`}>
                                  {outCount} →
                                </span>
                              )}
                            </div>
                          </div>

                          <div className="tier-card-title">{node.name}</div>
                          {node.properties?.framework && (
                            <div className="tier-framework">{node.properties.framework}</div>
                          )}

                          <div className="tier-card-actions">
                            {!isDb && (
                              <button
                                className="tier-flow-btn"
                                onClick={() => onFocusInFlow(node.name)}
                                title="Inspect in 3-column Project Flow"
                              >
                                🔀 Inspect Flow
                              </button>
                            )}
                            {node.filePath && (
                              <button
                                className="tier-code-btn"
                                onClick={() => onOpenFile(node.filePath!, node.lineStart)}
                                title="Open project file"
                              >
                                📄 Code
                              </button>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </section>

              {/* Inter-Layer Dependency Arrow */}
              {index < visibleLayers.length - 1 && (
                <div className="inter-layer-connector">
                  <div className="connector-line"></div>
                  <span className="connector-arrow">▼</span>
                  <div className="connector-line"></div>
                </div>
              )}
            </React.Fragment>
          );
        })}
      </div>
    </div>
  );
};
