import React, { useState, useEffect, useRef, useCallback } from 'react';
import mermaid from 'mermaid';
import { GraphData } from '../../../../proto/types';

interface MermaidDiagramViewProps {
  graph: GraphData | null;
  serverHttpUrl?: string;
  onOpenFile: (filePath: string, lineStart?: number) => void;
  onFocusNode?: (nodeId: string, kind?: string) => void;
}

export type DiagramType = 'architecture' | 'flow' | 'lineage' | 'cqrs';

export const MermaidDiagramView: React.FC<MermaidDiagramViewProps> = ({
  graph,
  serverHttpUrl,
  onOpenFile,
  onFocusNode,
}) => {
  const [diagramType, setDiagramType] = useState<DiagramType>('architecture');
  const [mermaidSource, setMermaidSource] = useState<string>('');
  const [svgHtml, setSvgHtml] = useState<string>('');
  const [showRawCode, setShowRawCode] = useState<boolean>(false);
  const [copied, setCopied] = useState<boolean>(false);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Pan & Zoom state
  const [zoom, setZoom] = useState<number>(1);
  const [pan, setPan] = useState<{ x: number; y: number }>({ x: 0, y: 0 });
  const isDraggingRef = useRef<boolean>(false);
  const dragStartRef = useRef<{ x: number; y: number }>({ x: 0, y: 0 });
  const containerRef = useRef<HTMLDivElement>(null);

  // Initialize mermaid on mount
  useEffect(() => {
    mermaid.initialize({
      startOnLoad: false,
      theme: 'dark',
      securityLevel: 'loose',
      fontFamily: 'var(--vscode-font-family, sans-serif)',
      flowchart: {
        useMaxWidth: false,
        htmlLabels: true,
        curve: 'basis',
      },
    });
  }, []);

  // Fallback: Generate Mermaid code client-side from graph data
  const generateClientMermaid = useCallback(
    (type: DiagramType): string => {
      if (!graph?.nodes) {
        return 'flowchart TD\n  empty["No graph nodes loaded"]';
      }

      const sanitize = (str: string) => str.replace(/[^a-zA-Z0-9_]/g, '_');
      const lines: string[] = ['flowchart TD'];

      if (type === 'architecture' || type === 'flow') {
        // 1. Projects
        const projects = graph.nodes.filter((n) => n.kind === 'Project');
        if (projects.length > 0) {
          lines.push('  subgraph Projects ["Applications & Services"]');
          for (const p of projects) {
            const id = sanitize(p.id);
            const label = p.displayName || p.name || 'Project';
            lines.push(`    ${id}["${label}"]`);
          }
          lines.push('  end');
        }

        // 2. Databases & Tables
        const dbs = graph.nodes.filter((n) => n.kind === 'Database' || n.kind === 'Table');
        if (dbs.length > 0) {
          lines.push('  subgraph Databases ["Data Storage"]');
          for (const d of dbs) {
            const id = sanitize(d.id);
            const label = d.displayName || d.name || 'Database';
            lines.push(`    ${id}[("🗄️ ${label}")]`);
          }
          lines.push('  end');
        }

        // 3. Topics
        const topics = graph.nodes.filter((n) => n.kind === 'Topic');
        if (topics.length > 0) {
          lines.push('  subgraph Brokers ["Message Brokers & Topics"]');
          for (const t of topics) {
            const id = sanitize(t.id);
            const label = t.displayName || t.name || 'Topic';
            lines.push(`    ${id}{{"📬 ${label}"}}`);
          }
          lines.push('  end');
        }

        // 4. External Services
        const exts = graph.nodes.filter((n) => n.kind === 'ExternalService' || n.kind === 'CloudService');
        if (exts.length > 0) {
          lines.push('  subgraph External ["External Services & APIs"]');
          for (const e of exts) {
            const id = sanitize(e.id);
            const label = e.displayName || e.name || 'External';
            lines.push(`    ${id}["☁️ ${label}"]`);
          }
          lines.push('  end');
        }

        // 5. Edges
        if (graph.edges) {
          const validIds = new Set(graph.nodes.map((n) => n.id));
          for (const edge of graph.edges) {
            if (validIds.has(edge.source) && validIds.has(edge.target)) {
              const src = sanitize(edge.source);
              const tgt = sanitize(edge.target);
              const kind = edge.kind || 'CALLS';
              lines.push(`    ${src} -->|${kind}| ${tgt}`);
            }
          }
        }
      } else if (type === 'lineage') {
        lines.push('  subgraph Lineage ["Data Lineage: Tables & Queries"]');
        const tables = graph.nodes.filter((n) => n.kind === 'Table' || n.kind === 'Database');
        for (const t of tables) {
          lines.push(`    ${sanitize(t.id)}[("🗄️ ${t.name}")]`);
        }
        lines.push('  end');
      } else if (type === 'cqrs') {
        lines.push('  subgraph EventPipeline ["Event Pipeline: Producers & Consumers"]');
        const topics = graph.nodes.filter((n) => n.kind === 'Topic');
        for (const t of topics) {
          lines.push(`    ${sanitize(t.id)}{{"📬 ${t.name}"}}`);
        }
        lines.push('  end');
      }

      return lines.join('\n');
    },
    [graph]
  );

  // Fetch or generate diagram source code
  const fetchDiagram = useCallback(async () => {
    setLoading(true);
    setError(null);

    let src = '';
    if (serverHttpUrl) {
      try {
        const url = `${serverHttpUrl}/api/diagram?type=${diagramType}&format=mermaid`;
        const res = await fetch(url);
        if (res.ok) {
          src = await res.text();
        }
      } catch (err: any) {
        console.warn('[MermaidDiagramView] Server fetch failed, generating client-side diagram:', err.message);
      }
    }

    if (!src || src.trim().length === 0) {
      src = generateClientMermaid(diagramType);
    }

    setMermaidSource(src);

    // Render Mermaid to SVG
    try {
      const renderId = `mermaid_svg_${Date.now()}`;
      const { svg } = await mermaid.render(renderId, src);
      setSvgHtml(svg);
    } catch (renderErr: any) {
      console.error('[MermaidDiagramView] Render error:', renderErr);
      setError(`Mermaid Syntax Render Error: ${renderErr.message || renderErr}`);
      setSvgHtml('');
    } finally {
      setLoading(false);
    }
  }, [serverHttpUrl, diagramType, generateClientMermaid]);

  useEffect(() => {
    fetchDiagram();
  }, [fetchDiagram]);

  const handleCopyCode = async () => {
    try {
      await navigator.clipboard.writeText(mermaidSource);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Copy failed:', err);
    }
  };

  // Pan & Zoom Handlers
  const handleWheel = (e: React.WheelEvent) => {
    e.preventDefault();
    const zoomFactor = e.deltaY < 0 ? 1.1 : 0.9;
    setZoom((prev) => Math.min(3.0, Math.max(0.3, prev * zoomFactor)));
  };

  const handleMouseDown = (e: React.MouseEvent) => {
    if (e.button !== 0) return; // Only left click
    isDraggingRef.current = true;
    dragStartRef.current = { x: e.clientX - pan.x, y: e.clientY - pan.y };
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    if (!isDraggingRef.current) return;
    setPan({
      x: e.clientX - dragStartRef.current.x,
      y: e.clientY - dragStartRef.current.y,
    });
  };

  const handleMouseUp = () => {
    isDraggingRef.current = false;
  };

  const handleResetView = () => {
    setZoom(1);
    setPan({ x: 0, y: 0 });
  };

  return (
    <div className="mermaid-view-container">
      {/* Top Diagram Toolbar */}
      <header className="mermaid-toolbar">
        <div className="mermaid-toolbar-left">
          <div className="diagram-type-tabs">
            <button
              className={`tab-btn ${diagramType === 'architecture' ? 'active' : ''}`}
              onClick={() => setDiagramType('architecture')}
              title="C1: System Context Diagram"
            >
              🌐 C1: System Context
            </button>
            <button
              className={`tab-btn ${diagramType === 'flow' ? 'active' : ''}`}
              onClick={() => setDiagramType('flow')}
              title="C2: Service Flow Diagram"
            >
              🔄 C2: Service Flow
            </button>
            <button
              className={`tab-btn ${diagramType === 'lineage' ? 'active' : ''}`}
              onClick={() => setDiagramType('lineage')}
              title="Data Lineage Diagram"
            >
              🗄️ Data Lineage
            </button>
            <button
              className={`tab-btn ${diagramType === 'cqrs' ? 'active' : ''}`}
              onClick={() => setDiagramType('cqrs')}
              title="CQRS & Events Diagram"
            >
              📬 Event Pipeline
            </button>
          </div>
        </div>

        <div className="mermaid-toolbar-right">
          {/* Zoom Controls */}
          <div className="zoom-controls">
            <button
              className="ctrl-btn"
              onClick={() => setZoom((z) => Math.min(3.0, z * 1.2))}
              title="Zoom In"
            >
              ➕
            </button>
            <span className="zoom-pct">{Math.round(zoom * 100)}%</span>
            <button
              className="ctrl-btn"
              onClick={() => setZoom((z) => Math.max(0.3, z / 1.2))}
              title="Zoom Out"
            >
              ➖
            </button>
            <button className="ctrl-btn" onClick={handleResetView} title="Reset Zoom and Pan">
              ↺ Reset
            </button>
          </div>

          <div className="toolbar-separator" />

          {/* Toggle Raw Code vs Rendered Diagram */}
          <button
            className={`action-btn ${showRawCode ? 'primary' : 'secondary'}`}
            onClick={() => setShowRawCode(!showRawCode)}
            title="Toggle between SVG diagram and Mermaid Markdown"
          >
            {showRawCode ? '📊 Diagram' : '📝 Markdown'}
          </button>

          {/* Copy Mermaid Code */}
          <button className="action-btn secondary" onClick={handleCopyCode} title="Copy Mermaid source to clipboard">
            {copied ? '✓ Copied!' : '📋 Copy'}
          </button>

          {/* Refresh Diagram */}
          <button className="action-btn secondary" onClick={fetchDiagram} title="Re-render diagram" disabled={loading}>
            {loading ? '⏳' : '🔄'} Refresh
          </button>
        </div>
      </header>

      {/* Main View Area */}
      <main className="mermaid-canvas-area" ref={containerRef} onWheel={handleWheel}>
        {loading && (
          <div className="mermaid-loading-overlay">
            <span className="loading-spinner">⚡</span>
            <span>Rendering Mermaid diagram...</span>
          </div>
        )}

        {error && (
          <div className="mermaid-error-banner">
            <span className="error-icon">⚠️</span>
            <div className="error-details">
              <strong>Diagram Rendering Notice:</strong>
              <p>{error}</p>
            </div>
            <button className="action-btn primary" onClick={() => setShowRawCode(true)}>
              Inspect Source
            </button>
          </div>
        )}

        {showRawCode ? (
          <div className="mermaid-code-viewer">
            <div className="code-viewer-header">
              <span>Mermaid Definition ({diagramType})</span>
              <button className="mini-btn" onClick={handleCopyCode}>
                {copied ? 'Copied' : 'Copy Source'}
              </button>
            </div>
            <pre className="mermaid-code-pre">
              <code>{mermaidSource}</code>
            </pre>
          </div>
        ) : (
          <div
            className="mermaid-svg-viewport"
            onMouseDown={handleMouseDown}
            onMouseMove={handleMouseMove}
            onMouseUp={handleMouseUp}
            onMouseLeave={handleMouseUp}
            style={{
              cursor: isDraggingRef.current ? 'grabbing' : 'grab',
            }}
          >
            <div
              className="mermaid-transform-layer"
              style={{
                transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom})`,
                transformOrigin: '0 0',
              }}
              dangerouslySetInnerHTML={{ __html: svgHtml }}
            />
          </div>
        )}
      </main>
    </div>
  );
};
