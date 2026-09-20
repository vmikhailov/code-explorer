import React, { useEffect, useRef } from 'react';
import cytoscape from 'cytoscape';
import dagre from 'cytoscape-dagre';
import { GraphData, GraphNode } from '../../../../proto/types';

cytoscape.use(dagre);

export interface CytoscapeViewProps {
  graph: GraphData | null;
  onOpenFile: (filePath: string, lineStart?: number) => void;
  onSelectNode: (node: GraphNode) => void;
}

export const CytoscapeView: React.FC<CytoscapeViewProps> = ({
  graph,
  onOpenFile,
  onSelectNode,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const cyRef = useRef<cytoscape.Core | null>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    const cy = cytoscape({
      container: containerRef.current,
      boxSelectionEnabled: false,
      style: [
        {
          selector: 'node',
          style: {
            'label': 'data(label)',
            'color': '#ffffff',
            'font-size': '11px',
            'text-valign': 'center',
            'text-halign': 'center',
            'background-color': '#334155',
            'border-width': 2,
            'border-color': '#64748b',
            'width': 'label',
            'height': 34,
            'padding': '10px',
            'shape': 'round-rectangle',
            'text-wrap': 'ellipsis',
            'text-max-width': '160px',
          },
        },
        {
          selector: 'node[kind = "Project"]',
          style: {
            'background-color': '#3b0764',
            'border-color': '#a855f7',
            'border-width': 3,
            'font-weight': 'bold',
            'height': 42,
            'font-size': '12px',
          },
        },
        {
          selector: 'node[kind = "Class"]',
          style: {
            'background-color': '#0c4a6e',
            'border-color': '#38bdf8',
          },
        },
        {
          selector: 'node[kind = "Database"], node[kind = "Table"]',
          style: {
            'background-color': '#064e3b',
            'border-color': '#34d399',
            'shape': 'barrel',
          },
        },
        {
          selector: 'node[kind = "Endpoint"]',
          style: {
            'background-color': '#78350f',
            'border-color': '#fbbf24',
            'shape': 'tag',
          },
        },
        {
          selector: 'node:selected',
          style: {
            'border-color': '#ffffff',
            'border-width': 4,
            'underlay-color': '#38bdf8',
            'underlay-padding': '4px',
            'underlay-opacity': 0.5,
          },
        },
        {
          selector: 'edge',
          style: {
            'width': 2,
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
          selector: 'edge:selected',
          style: {
            'width': 3,
            'line-color': '#38bdf8',
            'target-arrow-color': '#38bdf8',
          },
        },
      ],
    });

    cy.on('tap', 'node', (evt) => {
      const node = evt.target;
      onSelectNode(node.data() as GraphNode);
    });

    cy.on('dbltap', 'node', (evt) => {
      const node = evt.target;
      const data = node.data() as GraphNode;
      if (data.filePath) {
        onOpenFile(data.filePath, data.lineStart);
      }
    });

    cyRef.current = cy;

    return () => {
      cy.destroy();
      cyRef.current = null;
    };
  }, []);

  useEffect(() => {
    const cy = cyRef.current;
    if (!cy || !graph) return;

    cy.elements().remove();

    const elements: cytoscape.ElementDefinition[] = [];

    for (const n of graph.nodes || []) {
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
          parent: n.parentId,
        },
      });
    }

    for (const e of graph.edges || []) {
      elements.push({
        group: 'edges',
        data: {
          id: e.id || `${e.source}->${e.target}`,
          source: e.source,
          target: e.target,
          kind: e.kind,
          properties: e.properties,
        },
      });
    }

    cy.add(elements);
    const layout = cy.layout({
      name: 'dagre',
      rankDir: 'TB',
      nodeSep: 50,
      rankSep: 80,
      animate: true,
      animationDuration: 300,
    } as any);
    layout.run();
  }, [graph]);

  return (
    <div className="cytoscape-view-wrapper">
      <div ref={containerRef} className="cytoscape-viewport" />
    </div>
  );
};
