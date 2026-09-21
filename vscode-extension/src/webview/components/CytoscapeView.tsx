import React, { useEffect, useRef, useState, useMemo, useCallback } from 'react';
import cytoscape from 'cytoscape';
import dagre from 'cytoscape-dagre';
import { GraphData, GraphNode } from '../../../../proto/types';
import { LayerDefinition, getLayersFromGraph } from '../layers';

cytoscape.use(dagre);

export interface CytoscapeViewProps {
  graph: GraphData | null;
  onOpenFile: (filePath: string, lineStart?: number) => void;
  onSelectNode: (node: GraphNode) => void;
  groupLayers: boolean;
  onToggleGroupLayers: () => void;
  showTests: boolean;
  semanticOnly?: boolean;
}

export const CytoscapeView: React.FC<CytoscapeViewProps> = ({
  graph,
  onOpenFile,
  onSelectNode,
  groupLayers,
  onToggleGroupLayers,
  showTests,
  semanticOnly,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const cyRef = useRef<cytoscape.Core | null>(null);

  const [collapsedLayers, setCollapsedLayers] = useState<Set<string>>(new Set());

  const onSelectNodeRef = useRef(onSelectNode);
  onSelectNodeRef.current = onSelectNode;

  const onOpenFileRef = useRef(onOpenFile);
  onOpenFileRef.current = onOpenFile;

  // Extract layer definitions from graph metadata or standard defaults
  const layerDefs = useMemo(() => getLayersFromGraph(graph), [graph]);

  const toggleLayerCollapse = useCallback((layerId: string) => {
    setCollapsedLayers((prev) => {
      const next = new Set(prev);
      if (next.has(layerId)) {
        next.delete(layerId);
      } else {
        next.add(layerId);
      }
      return next;
    });
  }, []);

  const toggleLayerCollapseRef = useRef(toggleLayerCollapse);
  toggleLayerCollapseRef.current = toggleLayerCollapse;

  const collapseAllLayers = useCallback(() => {
    setCollapsedLayers(new Set(layerDefs.map((l) => l.layerId)));
  }, [layerDefs]);

  const expandAllLayers = useCallback(() => {
    setCollapsedLayers(new Set());
  }, []);

  const handleFit = useCallback(() => {
    if (cyRef.current) {
      cyRef.current.fit(undefined, 30);
    }
  }, []);

  // Compute active layers that have at least one node in the solution
  const activeLayers = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const n of graph?.nodes || []) {
      if (!showTests && (n.properties?.isTest === 'true' || n.properties?.layerId === 'layer_tests')) {
        continue;
      }
      const lid = n.properties?.layerId || 'layer_engines';
      counts[lid] = (counts[lid] || 0) + 1;
    }

    return layerDefs
      .filter((l) => counts[l.layerId] && counts[l.layerId] > 0)
      .map((l) => ({
        layer: l,
        count: counts[l.layerId],
      }));
  }, [graph, layerDefs, showTests]);

  // 1. Initialize Cytoscape instance
  useEffect(() => {
    if (!containerRef.current) return;

    const cy = cytoscape({
      container: containerRef.current,
      boxSelectionEnabled: false,
      style: [
        // Leaf / Childless Nodes (Projects, Classes, Databases, Endpoints)
        {
          selector: 'node:childless',
          style: {
            'label': 'data(label)',
            'color': '#ffffff',
            'font-size': '11px',
            'text-valign': 'center',
            'text-halign': 'center',
            'background-color': '#1e293b',
            'border-width': 2,
            'border-color': '#64748b',
            'width': 'label',
            'height': 34,
            'padding': '10px',
            'shape': 'round-rectangle',
            'text-wrap': 'ellipsis',
            'text-max-width': '170px',
          },
        },
        {
          selector: 'node:childless[kind = "Project"]',
          style: {
            'background-color': '#1e1b4b',
            'border-color': 'data(layerColor)',
            'border-width': 2,
            'font-weight': 'bold',
            'height': 38,
            'font-size': '11px',
          },
        },
        {
          selector: 'node:childless[kind = "Class"]',
          style: {
            'background-color': '#0c4a6e',
            'border-color': '#38bdf8',
          },
        },
        {
          selector: 'node:childless[kind = "Database"], node:childless[kind = "Table"]',
          style: {
            'background-color': '#064e3b',
            'border-color': '#34d399',
            'border-width': 2.5,
            'shape': 'barrel',
            'width': 'label',
            'height': 42,
            'padding': '14px',
            'font-weight': 'bold',
          },
        },
        {
          selector: 'node:childless[kind = "Endpoint"]',
          style: {
            'background-color': '#78350f',
            'border-color': '#fbbf24',
            'shape': 'tag',
          },
        },
        // Compound Parent Node (Expanded Layer Group)
        {
          selector: 'node:parent',
          style: {
            'background-color': 'data(layerBg)',
            'background-opacity': 0.08,
            'border-color': 'data(layerColor)',
            'border-width': 2,
            'border-style': 'dashed',
            'border-opacity': 0.7,
            'label': 'data(label)',
            'font-size': '12px',
            'font-weight': 'bold',
            'color': 'data(layerColor)',
            'text-valign': 'top',
            'text-halign': 'center',
            'text-margin-y': -8,
            'padding': '22px',
            'shape': 'round-rectangle',
          },
        },
        // Collapsed Layer Node (Summary Box)
        {
          selector: 'node[kind = "LayerCollapsed"]',
          style: {
            'background-color': '#181825',
            'border-color': 'data(layerColor)',
            'border-width': 2,
            'border-style': 'solid',
            'shape': 'round-rectangle',
            'label': 'data(label)',
            'color': '#ffffff',
            'font-size': '12px',
            'font-weight': 'bold',
            'text-valign': 'center',
            'text-halign': 'center',
            'text-wrap': 'wrap',
            'width': 'label',
            'height': 48,
            'padding': '16px',
          },
        },
        // Selection Highlighting
        {
          selector: 'node:selected',
          style: {
            'border-color': '#ffffff',
            'border-width': 3,
            'underlay-color': '#38bdf8',
            'underlay-padding': '4px',
            'underlay-opacity': 0.5,
          },
        },
        // Edges
        {
          selector: 'edge',
          style: {
            'width': 1.8,
            'line-color': '#475569',
            'target-arrow-color': '#475569',
            'target-arrow-shape': 'triangle',
            'curve-style': 'bezier',
            'label': 'data(kind)',
            'font-size': '9px',
            'color': '#94a3b8',
            'text-rotation': 'autorotate',
            'text-background-opacity': 0.8,
            'text-background-color': '#1e1e1e',
            'text-background-padding': '2px',
          },
        },
        {
          selector: "edge[depType = 'service_call'], edge[edgeKind = 'SERVICE_CALL']",
          style: {
            'line-color': '#38bdf8',
            'target-arrow-color': '#38bdf8',
            'target-arrow-shape': 'triangle-backcurve',
            'target-arrow-fill': 'filled',
            'arrow-scale': 1.25,
            'line-style': 'dashed',
            'line-dash-pattern': [6, 4],
            'width': 2.2,
          },
        },
        {
          selector: "edge[depType = 'library'], edge[edgeKind = 'LIBRARY']",
          style: {
            'line-color': '#34d399',
            'target-arrow-color': '#34d399',
            'target-arrow-shape': 'vee',
            'target-arrow-fill': 'hollow',
            'arrow-scale': 1.2,
            'line-style': 'solid',
            'width': 1.8,
          },
        },
        {
          selector: "edge[depType = 'database'], edge[edgeKind = 'USES_DB'], edge[kind = 'USES_DB']",
          style: {
            'line-color': '#34d399',
            'target-arrow-color': '#34d399',
            'target-arrow-shape': 'triangle',
            'target-arrow-fill': 'filled',
            'arrow-scale': 1.25,
            'line-style': 'solid',
            'width': 2.4,
            'label': 'uses db',
            'color': '#34d399',
            'font-weight': 'bold',
          },
        },
        {
          selector: "edge[depType = 'messaging'], edge[edgeKind = 'TRIGGERS']",
          style: {
            'line-color': '#fbbf24',
            'target-arrow-color': '#fbbf24',
            'target-arrow-shape': 'chevron',
            'arrow-scale': 1.15,
            'line-style': 'dashed',
            'line-dash-pattern': [8, 3, 2, 3],
            'width': 2,
          },
        },
        {
          selector: 'edge:selected',
          style: {
            'width': 3.5,
            'line-color': '#ffffff',
            'target-arrow-color': '#ffffff',
          },
        },
      ],
    });

    cy.on('tap', 'node', (evt) => {
      const node = evt.target;
      const data = node.data();

      // If clicked on collapsed layer box, expand it
      if (data.kind === 'LayerCollapsed') {
        if (data.layerId) {
          toggleLayerCollapseRef.current(data.layerId);
        }
        return;
      }

      // If clicked on parent compound node header, collapse it
      if (node.isParent()) {
        if (data.layerId) {
          toggleLayerCollapseRef.current(data.layerId);
        }
        return;
      }

      // Normal node selection
      onSelectNodeRef.current(data as GraphNode);
    });

    cy.on('dbltap', 'node', (evt) => {
      const node = evt.target;
      const data = node.data() as GraphNode;
      if (data.filePath) {
        onOpenFileRef.current(data.filePath, data.lineStart);
      }
    });

    cyRef.current = cy;

    return () => {
      cy.destroy();
      cyRef.current = null;
    };
  }, []);

  // 2. Build graph elements when graph, groupLayers, collapsedLayers, or showTests changes
  useEffect(() => {
    const cy = cyRef.current;
    if (!cy || !graph) return;

    cy.elements().remove();

    const elements: cytoscape.ElementDefinition[] = [];

    // Filter test nodes if showTests is false, and filter libraries if semanticOnly is true
    const validNodes = (graph.nodes || []).filter((n) => {
      if (!showTests && (n.properties?.isTest === 'true' || n.properties?.layerId === 'layer_tests')) {
        return false;
      }
      if (semanticOnly) {
        if (n.properties?.is_library === 'true' || n.properties?.entity_type === 'library') {
          return false;
        }
      }
      return true;
    });

    const validNodeIds = new Set(validNodes.map((n) => n.id));

    if (!groupLayers) {
      // Flat graph without grouping
      for (const n of validNodes) {
        const layerColor = n.properties?.layerColor || '#38bdf8';
        elements.push({
          group: 'nodes',
          data: {
            id: n.id,
            label: n.displayName || n.name,
            kind: n.kind,
            filePath: n.filePath,
            lineStart: n.lineStart,
            lineEnd: n.lineEnd,
            properties: n.properties,
            layerColor,
          },
        });
      }

      for (const e of graph.edges || []) {
        if (!validNodeIds.has(e.source) || !validNodeIds.has(e.target)) continue;
        if (semanticOnly && e.kind === 'LIBRARY') continue;

        const depType = (e.properties?.dependency_type || '').toLowerCase() ||
          (e.kind === 'SERVICE_CALL' || e.kind === 'CALLS_ENDPOINT' ? 'service_call' :
           e.kind === 'USES_DB' ? 'database' :
           e.kind === 'TRIGGERS' ? 'messaging' : 'library');

        elements.push({
          group: 'edges',
          data: {
            id: e.id || `${e.source}->${e.target}`,
            source: e.source,
            target: e.target,
            kind: e.kind,
            edgeKind: e.kind,
            depType,
            properties: e.properties,
          },
        });
      }
    } else {
      // Grouped by System Layers with Collapsing Support
      const nodeToLayer: Record<string, string> = {};
      const nodesByLayer = new Map<string, GraphNode[]>();

      for (const def of layerDefs) {
        nodesByLayer.set(def.layerId, []);
      }

      for (const node of validNodes) {
        const layerId = node.properties?.layerId || 'layer_engines';
        nodeToLayer[node.id] = layerId;

        const list = nodesByLayer.get(layerId) || [];
        list.push(node);
        nodesByLayer.set(layerId, list);
      }

      // Add Layer Nodes & Child Nodes
      for (const def of layerDefs) {
        const members = nodesByLayer.get(def.layerId) || [];
        if (members.length === 0) continue;

        const isCollapsed = collapsedLayers.has(def.layerId);

        if (isCollapsed) {
          // Collapsed Layer summary node
          elements.push({
            group: 'nodes',
            data: {
              id: def.layerId,
              label: `${def.icon} ${def.layerName}\n▶ [${members.length} projects collapsed]`,
              kind: 'LayerCollapsed',
              layerId: def.layerId,
              layerColor: def.color,
              layerBg: def.color,
            },
          });
        } else {
          // Expanded Compound Parent node
          elements.push({
            group: 'nodes',
            data: {
              id: def.layerId,
              label: `${def.icon} ${def.layerName} [${members.length}]`,
              kind: 'LayerGroup',
              layerId: def.layerId,
              layerColor: def.color,
              layerBg: def.color,
              isParent: true,
            },
          });

          // Add member child nodes
          for (const n of members) {
            elements.push({
              group: 'nodes',
              data: {
                id: n.id,
                label: n.displayName || n.name,
                kind: n.kind,
                filePath: n.filePath,
                lineStart: n.lineStart,
                lineEnd: n.lineEnd,
                properties: n.properties,
                parent: def.layerId,
                layerId: def.layerId,
                layerColor: def.color,
              },
            });
          }
        }
      }

      // Add Edges with Collapsed Re-routing and Aggregation
      const edgeMap = new Map<string, { source: string; target: string; count: number; kinds: Set<string>; depTypes: Set<string>; primaryKind: string }>();

      for (const e of graph.edges || []) {
        if (!validNodeIds.has(e.source) || !validNodeIds.has(e.target)) continue;

        const sourceLayer = nodeToLayer[e.source];
        const targetLayer = nodeToLayer[e.target];

        const effectiveSource = (sourceLayer && collapsedLayers.has(sourceLayer))
          ? sourceLayer
          : e.source;

        const effectiveTarget = (targetLayer && collapsedLayers.has(targetLayer))
          ? targetLayer
          : e.target;

        // Skip internal self-loops within the same collapsed layer
        if (effectiveSource === effectiveTarget) {
          continue;
        }

        const depType = (e.properties?.dependency_type || '').toLowerCase() ||
          (e.kind === 'SERVICE_CALL' || e.kind === 'CALLS_ENDPOINT' ? 'service_call' :
           e.kind === 'USES_DB' ? 'database' :
           e.kind === 'TRIGGERS' ? 'messaging' : 'library');

        const key = `${effectiveSource}->${effectiveTarget}`;
        if (!edgeMap.has(key)) {
          edgeMap.set(key, {
            source: effectiveSource,
            target: effectiveTarget,
            count: 1,
            kinds: new Set([e.kind]),
            depTypes: new Set([depType]),
            primaryKind: e.kind,
          });
        } else {
          const existing = edgeMap.get(key)!;
          existing.count += 1;
          existing.kinds.add(e.kind);
          existing.depTypes.add(depType);
        }
      }

      for (const [key, agg] of edgeMap) {
        const isAggregated = agg.count > 1;
        const label = isAggregated
          ? `${agg.count} calls`
          : Array.from(agg.kinds)[0] || 'calls';

        const primaryDep = agg.depTypes.has('service_call')
          ? 'service_call'
          : agg.depTypes.has('database')
          ? 'database'
          : agg.depTypes.has('messaging')
          ? 'messaging'
          : 'library';

        elements.push({
          group: 'edges',
          data: {
            id: key,
            source: agg.source,
            target: agg.target,
            kind: label,
            edgeKind: agg.primaryKind,
            depType: primaryDep,
          },
        });
      }
    }

    cy.add(elements);

    const layout = cy.layout({
      name: 'dagre',
      rankDir: 'TB',
      nodeSep: semanticOnly ? 60 : 40,
      rankSep: semanticOnly ? 80 : 60,
      animate: true,
      animationDuration: 300,
    } as any);

    layout.run();
  }, [graph, groupLayers, collapsedLayers, showTests, layerDefs, semanticOnly]);

  return (
    <div className="cytoscape-view-wrapper">
      <div ref={containerRef} className="cytoscape-viewport" />

      {/* Floating System Layers HUD inside Cytoscape canvas */}
      {groupLayers && activeLayers.length > 0 && (
        <div className="cytoscape-layers-hud">
          <div className="hud-header">
            <span className="hud-title">🏛️ System Layers</span>
            <div className="hud-actions">
              <button
                className="hud-action-btn"
                onClick={collapseAllLayers}
                title="Collapse all layers into summary boxes"
              >
                Collapse All
              </button>
              <button
                className="hud-action-btn"
                onClick={expandAllLayers}
                title="Expand all layers"
              >
                Expand All
              </button>
              <button
                className="hud-action-btn"
                onClick={handleFit}
                title="Fit diagram to view"
              >
                Fit
              </button>
            </div>
          </div>
          <div className="hud-chips-list">
            {activeLayers.map(({ layer, count }) => {
              const isCollapsed = collapsedLayers.has(layer.layerId);
              return (
                <button
                  key={layer.layerId}
                  className={`hud-layer-chip ${isCollapsed ? 'collapsed' : 'expanded'}`}
                  style={{
                    borderColor: `${layer.color}66`,
                    backgroundColor: isCollapsed ? `${layer.color}15` : `${layer.color}25`,
                  }}
                  onClick={() => toggleLayerCollapse(layer.layerId)}
                  title={isCollapsed ? `Click to expand ${layer.layerName}` : `Click to collapse ${layer.layerName}`}
                >
                  <span className="chip-icon">{layer.icon}</span>
                  <span className="chip-name">{layer.layerName}</span>
                  <span className="chip-count" style={{ color: layer.color }}>{count}</span>
                  <span className="chip-toggle-arrow">{isCollapsed ? '▶' : '▼'}</span>
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
};
