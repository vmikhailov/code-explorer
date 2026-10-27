import React, { useState, useEffect, useRef, useMemo, useCallback } from 'react';
import mermaid from 'mermaid';
import { SelectedNodeDetail } from '../../components/DomainArchitectureView';
import { calculateNormalizedZoomFactor, calculateAnchorPan } from '../../utils/wheelZoom';

export interface DomainMermaidViewProps {
  nodes: SelectedNodeDetail[];
  edges: Array<{
    source: string;
    target: string;
    label?: string;
    category?: string;
    count?: number;
    kind?: string;
  }>;
  selectedNodeId?: string;
  onSelectNode?: (nodeId: string) => void;
  title?: string;
}

export const DomainMermaidView: React.FC<DomainMermaidViewProps> = ({
  nodes,
  edges,
  selectedNodeId,
  onSelectNode,
  title = 'Mermaid Architecture Flowchart',
}) => {
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
  const zoomRef = useRef(zoom);
  zoomRef.current = zoom;
  const panRef = useRef(pan);
  panRef.current = pan;

  // Initialize mermaid on mount
  useEffect(() => {
    mermaid.initialize({
      startOnLoad: false,
      theme: 'dark',
      securityLevel: 'loose',
      fontFamily: 'var(--vscode-font-family, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif)',
      flowchart: {
        useMaxWidth: false,
        htmlLabels: true,
        curve: 'basis',
      },
    });
  }, []);

  // Generate real Mermaid flowchart syntax from nodes & edges
  const { mermaidSource, idToRealIdMap } = useMemo(() => {
    const sanitize = (id: string) => 'n_' + id.replace(/[^a-zA-Z0-9_]/g, '_');
    const esc = (text: string) => (text || '').replace(/"/g, "'").replace(/\n/g, ' ');

    if (!nodes || nodes.length === 0) {
      return {
        mermaidSource: 'flowchart TD\n  empty["No visible entities loaded"]',
        idToRealIdMap: new Map<string, string>(),
      };
    }

    const mapping = new Map<string, string>();
    nodes.forEach((n) => {
      mapping.set(sanitize(n.id), n.id);
    });

    const lines: string[] = ['flowchart TD'];

    // Classify nodes into clean subgraphs
    const ingressNodes = nodes.filter((n) => {
      const k = (n.kind || '').toLowerCase();
      return k === 'endpoint' || k === 'entrypoint' || k === 'ingress' || k === 'app' || k === 'frontendapp';
    });

    const serviceNodes = nodes.filter((n) => {
      const k = (n.kind || '').toLowerCase();
      return (
        k === 'service' ||
        k === 'worker' ||
        k === 'class' ||
        k === 'interface' ||
        k === 'record' ||
        k === 'component' ||
        k === 'clitool'
      );
    });

    const foundationNodes = nodes.filter((n) => {
      const k = (n.kind || '').toLowerCase();
      return k === 'library' || k === 'package' || k === 'module' || k === 'testsuite';
    });

    const databaseNodes = nodes.filter((n) => {
      const k = (n.kind || '').toLowerCase();
      return k === 'database' || k === 'table' || k === 'sqlfile' || k === 'migration';
    });

    const topicNodes = nodes.filter((n) => {
      const k = (n.kind || '').toLowerCase();
      return k === 'topic' || k === 'queue' || k === 'event';
    });

    const externalNodes = nodes.filter((n) => {
      const k = (n.kind || '').toLowerCase();
      return k === 'externalservice' || k === 'cloudservice' || k === 'external';
    });

    // Subgraphs formatting
    if (ingressNodes.length > 0) {
      lines.push('  subgraph Ingress ["🌐 Endpoints & Ingress"]');
      for (const n of ingressNodes) {
        lines.push(`    ${sanitize(n.id)}(["🌐 ${esc(n.displayName || n.name)}"])`);
      }
      lines.push('  end');
    }

    if (serviceNodes.length > 0) {
      lines.push('  subgraph Services ["⚙️ Application & Domain Core"]');
      for (const n of serviceNodes) {
        lines.push(`    ${sanitize(n.id)}["${esc(n.displayName || n.name)}"]`);
      }
      lines.push('  end');
    }

    if (foundationNodes.length > 0) {
      lines.push('  subgraph Foundation ["🧰 Foundation & Utilities"]');
      for (const n of foundationNodes) {
        lines.push(`    ${sanitize(n.id)}["📦 ${esc(n.displayName || n.name)}"]`);
      }
      lines.push('  end');
    }

    if (databaseNodes.length > 0) {
      lines.push('  subgraph Storage ["🗄️ Databases & Storage"]');
      for (const n of databaseNodes) {
        lines.push(`    ${sanitize(n.id)}[("🗄️ ${esc(n.displayName || n.name)}")]`);
      }
      lines.push('  end');
    }

    if (topicNodes.length > 0) {
      lines.push('  subgraph Messaging ["📬 Message Brokers & Topics"]');
      for (const n of topicNodes) {
        lines.push(`    ${sanitize(n.id)}[/"📬 ${esc(n.displayName || n.name)}"\\]`);
      }
      lines.push('  end');
    }

    if (externalNodes.length > 0) {
      lines.push('  subgraph External ["☁️ External Services & APIs"]');
      for (const n of externalNodes) {
        lines.push(`    ${sanitize(n.id)}["☁️ ${esc(n.displayName || n.name)}"]`);
      }
      lines.push('  end');
    }

    // Remaining nodes
    const categorized = new Set([
      ...ingressNodes,
      ...serviceNodes,
      ...foundationNodes,
      ...databaseNodes,
      ...topicNodes,
      ...externalNodes,
    ]);
    const remaining = nodes.filter((n) => !categorized.has(n));
    if (remaining.length > 0) {
      lines.push('  subgraph Other ["Entities"]');
      for (const n of remaining) {
        lines.push(`    ${sanitize(n.id)}["${esc(n.displayName || n.name)}"]`);
      }
      lines.push('  end');
    }

    // Edges
    const validIds = new Set(nodes.map((n) => n.id));
    for (const e of edges) {
      if (validIds.has(e.source) && validIds.has(e.target)) {
        const src = sanitize(e.source);
        const tgt = sanitize(e.target);
        const cat = (e.category || '').toLowerCase();
        const arrow = cat === 'messaging' ? '-.->' : cat === 'database' ? '==>' : '-->';
        const rawLabel = e.label || e.kind || '';
        const countSuffix = e.count && e.count > 1 ? ` (${e.count})` : '';
        const label = rawLabel ? `|"${esc(rawLabel + countSuffix)}"|` : '';
        lines.push(`  ${src} ${arrow}${label} ${tgt}`);
      }
    }

    return {
      mermaidSource: lines.join('\n'),
      idToRealIdMap: mapping,
    };
  }, [nodes, edges]);

  // Render Mermaid source to SVG
  useEffect(() => {
    let isMounted = true;
    setLoading(true);
    setError(null);

    const renderId = `domain_mermaid_${Date.now()}_${Math.random().toString(36).substring(2, 6)}`;
    mermaid
      .render(renderId, mermaidSource)
      .then(({ svg }) => {
        if (!isMounted) return;
        setSvgHtml(svg);
        setError(null);
        setLoading(false);
      })
      .catch((err) => {
        if (!isMounted) return;
        console.error('[DomainMermaidView] Render error:', err);
        setError(`Mermaid Syntax Render Error: ${err.message || String(err)}`);
        setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [mermaidSource]);

  // Pan & Zoom handlers
  const handleWheel = (e: React.WheelEvent) => {
    e.preventDefault();
    const container = containerRef.current;
    if (!container) return;

    const currentZoom = zoomRef.current;
    const currentPan = panRef.current;

    const factor = calculateNormalizedZoomFactor(e, 1.0);
    const newZoom = Math.min(3.5, Math.max(0.15, currentZoom * factor));

    const rect = container.getBoundingClientRect();
    const newPan = calculateAnchorPan(
      e.clientX,
      e.clientY,
      rect,
      currentPan,
      currentZoom,
      newZoom
    );

    zoomRef.current = newZoom;
    panRef.current = newPan;
    setZoom(newZoom);
    setPan(newPan);
  };

  const handleMouseDown = (e: React.MouseEvent) => {
    if (e.button !== 0) return;
    isDraggingRef.current = true;
    dragStartRef.current = {
      x: e.clientX - panRef.current.x,
      y: e.clientY - panRef.current.y,
    };
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    if (!isDraggingRef.current) return;
    const newPan = {
      x: e.clientX - dragStartRef.current.x,
      y: e.clientY - dragStartRef.current.y,
    };
    panRef.current = newPan;
    setPan(newPan);
  };

  const handleMouseUp = () => {
    isDraggingRef.current = false;
  };

  const handleResetZoom = () => {
    zoomRef.current = 1;
    panRef.current = { x: 0, y: 0 };
    setZoom(1);
    setPan({ x: 0, y: 0 });
  };

  const handleCopyCode = async () => {
    try {
      await navigator.clipboard.writeText(mermaidSource);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      console.error('Failed to copy Mermaid code:', err);
    }
  };

  // Node selection via SVG click
  const handleSvgClick = useCallback(
    (e: React.MouseEvent) => {
      if (!onSelectNode) return;
      const target = e.target as HTMLElement | null;
      if (!target) return;
      const nodeG = target.closest('g.node');
      if (!nodeG) return;
      const gId = nodeG.id || '';
      for (const [sanitized, realId] of idToRealIdMap.entries()) {
        if (gId.includes(sanitized)) {
          onSelectNode(realId);
          break;
        }
      }
    },
    [onSelectNode, idToRealIdMap]
  );

  return (
    <div className="mermaid-view-container" style={{ position: 'relative', width: '100%', height: '100%' }}>
      {/* Top Floating Control Bar */}
      <div
        className="mermaid-toolbar"
        style={{
          position: 'absolute',
          top: 10,
          right: 14,
          zIndex: 10,
          borderRadius: '6px',
          boxShadow: '0 4px 16px rgba(0, 0, 0, 0.5)',
        }}
      >
        <div className="mermaid-toolbar-left">
          <span style={{ fontSize: '11px', fontWeight: 600, color: '#38bdf8' }}>
            🧜 Mermaid
          </span>
          <span style={{ fontSize: '10px', color: '#64748b' }}>
            ({nodes.length} nodes, {edges.length} edges)
          </span>
        </div>

        <div className="mermaid-toolbar-right">
          {/* Zoom controls */}
          <div className="zoom-controls">
            <button
              type="button"
              className="ctrl-btn"
              onClick={() => {
                const next = Math.max(0.15, +(zoom - 0.2).toFixed(2));
                zoomRef.current = next;
                setZoom(next);
              }}
              title="Zoom Out"
            >
              −
            </button>
            <span
              className="zoom-pct"
              onClick={handleResetZoom}
              title="Click to reset zoom to 100%"
            >
              {Math.round(zoom * 100)}%
            </span>
            <button
              type="button"
              className="ctrl-btn"
              onClick={() => {
                const next = Math.min(3.5, +(zoom + 0.2).toFixed(2));
                zoomRef.current = next;
                setZoom(next);
              }}
              title="Zoom In"
            >
              +
            </button>
            <button
              type="button"
              className="ctrl-btn reset-btn"
              onClick={handleResetZoom}
              title="Reset Zoom & Pan"
            >
              Reset
            </button>
          </div>

          <div className="toolbar-separator" />

          {/* Toggle Code / Diagram */}
          <button
            type="button"
            className={`action-btn ${showRawCode ? 'primary' : 'secondary'}`}
            onClick={() => setShowRawCode((v) => !v)}
            title="Toggle between SVG diagram and Mermaid Markdown"
          >
            {showRawCode ? '👁️ Diagram' : '{ } Code'}
          </button>

          {/* Copy Mermaid Code */}
          <button
            type="button"
            className={`action-btn ${copied ? 'copied' : 'secondary'}`}
            onClick={handleCopyCode}
            title="Copy Mermaid source to clipboard"
          >
            {copied ? '✓ Copied' : '📋 Copy'}
          </button>
        </div>
      </div>

      {/* Main View Area */}
      <main
        className="mermaid-canvas-area"
        ref={containerRef}
        onWheel={handleWheel}
        onMouseDown={handleMouseDown}
        onMouseMove={handleMouseMove}
        onMouseUp={handleMouseUp}
        onClick={handleSvgClick}
        style={{ cursor: isDraggingRef.current ? 'grabbing' : 'grab' }}
      >
        {loading && (
          <div className="mermaid-loading-overlay">
            <div className="view-loading-spinner" style={{ width: 14, height: 14, borderWidth: 2 }} />
            <span>Rendering Mermaid diagram...</span>
          </div>
        )}

        {error && (
          <div className="mermaid-error-banner">
            <span className="error-icon">⚠️</span>
            <div className="error-details">
              <strong>Render Error</strong>
              <p>{error}</p>
            </div>
          </div>
        )}

        {showRawCode ? (
          <div className="mermaid-code-viewer">
            <div className="code-viewer-header">
              <span>Mermaid Definition ({nodes.length} nodes)</span>
              <button
                type="button"
                className={`mini-btn ${copied ? 'copied' : ''}`}
                onClick={handleCopyCode}
              >
                {copied ? '✓ Copied' : 'Copy'}
              </button>
            </div>
            <pre className="mermaid-code-pre">
              <code>{mermaidSource}</code>
            </pre>
          </div>
        ) : (
          <div className="mermaid-svg-viewport">
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
