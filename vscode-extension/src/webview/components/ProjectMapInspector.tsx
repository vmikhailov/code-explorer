import React from 'react';
import {
  ProjectMapEdgeModel,
  ProjectMapModel,
  ProjectMapNodeModel,
  TIER_COLORS,
  resourceBadge,
} from '../projectMap/projectMapModel';

export interface ProjectMapInspectorProps {
  model: ProjectMapModel;
  node?: ProjectMapNodeModel;
  edge?: ProjectMapEdgeModel;
  onClose: () => void;
  onOpenFile: (filePath: string, lineStart?: number) => void;
  onSelectNode: (id: string) => void;
  onSelectEdge: (id: string) => void;
}

const fileName = (p: string) => p.slice(p.lastIndexOf('/') + 1);

export const ProjectMapInspector: React.FC<ProjectMapInspectorProps> = ({
  model,
  node,
  edge,
  onClose,
  onOpenFile,
  onSelectNode,
  onSelectEdge,
}) => {
  const nameOf = (id: string) => model.nodes.find((n) => n.id === id)?.name ?? id;

  return (
    <aside className="pm-inspector">
      <header className="pm-inspector-header">
        <span className="pm-inspector-title">{node ? node.name : edge ? 'Dependency' : ''}</span>
        <button className="pm-icon-btn" onClick={onClose} title="Close inspector">
          ✕
        </button>
      </header>
      <div className="pm-inspector-body">
        {node && (
          <NodeDetails
            model={model}
            node={node}
            nameOf={nameOf}
            onOpenFile={onOpenFile}
            onSelectNode={onSelectNode}
            onSelectEdge={onSelectEdge}
          />
        )}
        {edge && <EdgeDetails edge={edge} nameOf={nameOf} onOpenFile={onOpenFile} onSelectNode={onSelectNode} />}
      </div>
    </aside>
  );
};

const NodeDetails: React.FC<{
  model: ProjectMapModel;
  node: ProjectMapNodeModel;
  nameOf: (id: string) => string;
  onOpenFile: (filePath: string, lineStart?: number) => void;
  onSelectNode: (id: string) => void;
  onSelectEdge: (id: string) => void;
}> = ({ model, node, nameOf, onOpenFile, onSelectNode, onSelectEdge }) => {
  const outgoing = model.edges.filter((e) => e.source === node.id).sort((a, b) => b.count - a.count);
  const incoming = model.edges.filter((e) => e.target === node.id).sort((a, b) => b.count - a.count);
  const color = TIER_COLORS[node.tier] ?? '#888';

  return (
    <>
      <div className="pm-tier-chip" style={{ borderColor: color, color }}>
        {node.isResource ? `${resourceBadge(node.resourceKind)} · ${node.resourceKind}` : node.tierLabel}
      </div>
      {node.tierReason && <p className="pm-muted">{node.tierReason}</p>}
      {node.cycleGroup !== undefined && (
        <p className="pm-warning">⚠️ Part of dependency cycle #{node.cycleGroup}</p>
      )}

      {!node.isResource && (
        <dl className="pm-metrics">
          <dt>Files</dt>
          <dd>{node.fileCount}</dd>
          <dt>Lines</dt>
          <dd>{node.lineCount.toLocaleString()}</dd>
          <dt>Used by</dt>
          <dd>{node.fanIn} module(s)</dd>
          <dt>Depends on</dt>
          <dd>{node.fanOut} module(s)</dd>
          <dt>Instability</dt>
          <dd>{node.instability.toFixed(2)}</dd>
          <dt>Entry points</dt>
          <dd>{node.entryPoints}</dd>
          {node.dataFileCount > 0 && (
            <>
              <dt>Data access</dt>
              <dd>{node.dataFileCount} file(s)</dd>
            </>
          )}
        </dl>
      )}

      {node.path && <p className="pm-path" title={node.path}>{node.path}</p>}

      <DependencyList title="Depends on" edges={outgoing} labelOf={(e) => nameOf(e.target)} onSelectEdge={onSelectEdge} />
      <DependencyList title="Used by" edges={incoming} labelOf={(e) => nameOf(e.source)} onSelectEdge={onSelectEdge} />

      {node.files.length > 0 && (
        <section className="pm-section">
          <h4>
            Files {node.fileCount > node.files.length ? `(first ${node.files.length} of ${node.fileCount})` : ''}
          </h4>
          <ul className="pm-list">
            {node.files.map((f) => (
              <li key={f}>
                <button className="pm-link" onClick={() => onOpenFile(f)} title={f}>
                  {fileName(f)}
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}

      {node.isResource && incoming.length > 0 && (
        <section className="pm-section">
          <h4>Accessed from</h4>
          <ul className="pm-list">
            {incoming.map((e) => (
              <li key={e.id}>
                <button className="pm-link" onClick={() => onSelectNode(e.source)}>
                  {nameOf(e.source)}
                </button>
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  );
};

const DependencyList: React.FC<{
  title: string;
  edges: ProjectMapEdgeModel[];
  labelOf: (e: ProjectMapEdgeModel) => string;
  onSelectEdge: (id: string) => void;
}> = ({ title, edges, labelOf, onSelectEdge }) => {
  if (edges.length === 0) return null;
  return (
    <section className="pm-section">
      <h4>
        {title} ({edges.length})
      </h4>
      <ul className="pm-list">
        {edges.map((e) => (
          <li key={e.id} className={e.isCircular ? 'is-circular' : ''}>
            <button className="pm-link" onClick={() => onSelectEdge(e.id)}>
              {e.isCircular ? '🔄 ' : ''}
              {labelOf(e)}
            </button>
            <span className="pm-count">{e.count}</span>
          </li>
        ))}
      </ul>
    </section>
  );
};

const EdgeDetails: React.FC<{
  edge: ProjectMapEdgeModel;
  nameOf: (id: string) => string;
  onOpenFile: (filePath: string, lineStart?: number) => void;
  onSelectNode: (id: string) => void;
}> = ({ edge, nameOf, onOpenFile, onSelectNode }) => (
  <>
    <p className="pm-edge-title">
      <button className="pm-link" onClick={() => onSelectNode(edge.source)}>
        {nameOf(edge.source)}
      </button>
      <span> → </span>
      <button className="pm-link" onClick={() => onSelectNode(edge.target)}>
        {nameOf(edge.target)}
      </button>
    </p>
    {edge.isCircular && <p className="pm-warning">🔄 This dependency is part of a cycle</p>}
    <dl className="pm-metrics">
      <dt>References</dt>
      <dd>{edge.count}</dd>
      <dt>Files involved</dt>
      <dd>{edge.distinctFiles}</dd>
      {Object.entries(edge.kinds).map(([k, v]) => (
        <React.Fragment key={k}>
          <dt>{k}</dt>
          <dd>{v}</dd>
        </React.Fragment>
      ))}
    </dl>
    {edge.samples.length > 0 && (
      <section className="pm-section">
        <h4>
          Evidence {edge.count > edge.samples.length ? `(${edge.samples.length} of ${edge.count})` : ''}
        </h4>
        <ul className="pm-list pm-evidence">
          {edge.samples.map((s, i) => (
            <li key={`${s.file}:${s.line ?? 0}:${i}`}>
              <button
                className="pm-link"
                onClick={() => onOpenFile(s.file, s.line ?? undefined)}
                title={`${s.file}${s.line ? `:${s.line}` : ''}`}
              >
                <code>
                  {s.from ?? '?'} → {s.to ?? '?'}
                </code>
                <span className="pm-muted">
                  {' '}
                  {fileName(s.file)}
                  {s.line ? `:${s.line}` : ''} · {s.kind}
                </span>
              </button>
            </li>
          ))}
        </ul>
      </section>
    )}
  </>
);
