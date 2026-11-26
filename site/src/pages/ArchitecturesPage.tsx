import React, { useMemo, useState } from 'react';
import {
  ARCHITECTURE_PACKS,
  ArchGateSpec,
  ArchPackSpec,
  STACK_CONFIGS,
  StackKey,
} from '../data/architecturesCatalog';

type EditorTabKey = 'overview' | 'topology' | 'yaml';
type BottomTabKey = 'problems' | 'terminal';

const STACK_KEYS: StackKey[] = ['agnostic', 'dotnet', 'typescript', 'go', 'python', 'java'];

function buildGateYaml(g: ArchGateSpec): string {
  const parts = g.coords.split(' x ');
  return [
    `id: ${g.id}`,
    `category: Architectural`,
    `platform: ${g.platform}`,
    `scope: ${parts[0] || 'Module'}`,
    `dimension: ${parts[1] || 'Boundaries'}`,
    `severity: ${g.severity}`,
    `name: ${g.name}`,
    `description: ${g.description}`,
    `remediation: ${g.remediation}`,
    `target: "${g.target}"`,
    `message: "${g.message}"`,
    `query: |-`,
    `  ${g.query}`,
    `ratchet:`,
    `  enabled: true`,
    `  baseline: baselines/${g.id}.json`,
  ].join('\n');
}

function buildPackBundleYaml(pack: ArchPackSpec, stack: StackKey, gates: ArchGateSpec[]): string {
  const header = [
    `# CodeExplorer Semantic Invariant Gate Pack (v2 Node-First & Edge-Aware)`,
    `# Pack: ${pack.slug} | Stack: ${stack}`,
    `# Save to .codeexplorer/gates/${pack.slug}.yaml and run: ce gate check`,
    `---`,
  ].join('\n');
  return `${header}\n${gates.map(buildGateYaml).join('\n---\n')}\n`;
}

interface OverviewTabProps {
  pack: ArchPackSpec;
  gatesCount: number;
  stackLabel: string;
  onOpenTopology: () => void;
  onOpenAllYaml: () => void;
}

const OverviewTabContent: React.FC<OverviewTabProps> = ({
  pack,
  gatesCount,
  stackLabel,
  onOpenTopology,
  onOpenAllYaml,
}) => (
  <article className="ide-doc">
    <header className="ide-doc-header">
      <div className="ide-doc-badges">
        <code className="ide-badge ide-badge--accent">
          {pack.category === 'structural' ? 'Structural Style' : 'Runtime & Dataflow Invariant'}
        </code>
        <code className="ide-badge">{gatesCount} Semantic Invariant Gates</code>
        <code className="ide-badge">Preset: {stackLabel}</code>
      </div>
      <h2 className="ide-doc-title">{pack.title}</h2>
      <p className="ide-doc-tagline">{pack.tagline}</p>
    </header>

    <div className="ide-callout ide-callout--invariant">
      <div className="ide-callout-label">THE CORE INVARIANT</div>
      <p className="ide-callout-text">{pack.coreInvariant}</p>
    </div>

    <section className="ide-doc-section">
      <h3 className="ide-doc-h3">1. Why This Architecture Matters in Production</h3>
      <div className="ide-prose-stack">
        {pack.whyItMatters.map((para, idx) => (
          <p key={idx}>{para}</p>
        ))}
      </div>
    </section>

    <section className="ide-doc-section">
      <h3 className="ide-doc-h3">2. How It Erodes in Practice (and Under AI Coding Agents)</h3>
      <div className="ide-erosion-grid">
        {pack.howItErodes.map((er, idx) => (
          <div key={idx} className="ide-erosion-card">
            <div className="ide-erosion-head">
              <span className="ide-erosion-num">FAILURE MODE 0{idx + 1}</span>
              <h4 className="ide-erosion-title">{er.title}</h4>
            </div>
            <p className="ide-erosion-desc">{er.scenario}</p>
            <pre className="ide-code-snippet">
              <code>{er.codeExample}</code>
            </pre>
          </div>
        ))}
      </div>
    </section>

    <section className="ide-doc-section">
      <h3 className="ide-doc-h3">
        3. Why File-Level Linters &amp; ArchUnit Miss It (5-Layer Graph Advantage)
      </h3>
      <div className="ide-callout ide-callout--graph">
        <p className="ide-callout-text">{pack.whyGraphBeatsLinters}</p>
      </div>
    </section>

    <section className="ide-doc-section">
      <h3 className="ide-doc-h3">4. Engineering Trade-Offs: When to Use vs When to Skip</h3>
      <div className="ide-tradeoff-grid">
        <div className="ide-tradeoff-box ide-tradeoff-box--fit">
          <div className="ide-tradeoff-head">✓ Best Fit When</div>
          <ul className="ide-bullet-list">
            {pack.bestFit.map((item, idx) => (
              <li key={idx}>{item}</li>
            ))}
          </ul>
        </div>
        <div className="ide-tradeoff-box ide-tradeoff-box--avoid">
          <div className="ide-tradeoff-head">✕ Avoid or Keep Simple When</div>
          <ul className="ide-bullet-list">
            {pack.avoidWhen.map((item, idx) => (
              <li key={idx}>{item}</li>
            ))}
          </ul>
        </div>
      </div>
    </section>

    <footer className="ide-doc-footer">
      <button type="button" className="btn" onClick={onOpenTopology}>
        Inspect Layer Topology &amp; Forbidden Edges →
      </button>
      <button type="button" className="btn btn--primary" onClick={onOpenAllYaml}>
        View Executable {pack.slug}.gates.yaml ({gatesCount} gates) →
      </button>
    </footer>
  </article>
);

interface TopologyTabProps {
  pack: ArchPackSpec;
  layers: ReturnType<ArchPackSpec['layers']>;
  gates: ArchGateSpec[];
  stackLabel: string;
  onInspectGate: (gateId: string) => void;
}

const TopologyTabContent: React.FC<TopologyTabProps> = ({
  pack,
  layers,
  gates,
  stackLabel,
  onInspectGate,
}) => (
  <div className="ide-topology-view">
    <div className="ide-topo-split">
      <div className="ide-topo-diagram-box">
        <div className="ide-box-head">
          <div>
            <h3 className="ide-box-title">Layer &amp; Boundary Blueprint</h3>
            <p className="ide-box-sub">
              Stack paths adapted for <code>{stackLabel}</code>
            </p>
          </div>
          <div className="blueprint-legend">
            <code className="legend-tag legend-tag--allow">ALLOWED</code>
            <code className="legend-tag legend-tag--block">BLOCKED BY GATE</code>
          </div>
        </div>

        <div className="ide-layers-stack">
          {layers.map((layer, idx) => (
            <div key={idx} className={`ide-layer-card${layer.core ? ' is-core' : ''}`}>
              <div className="ide-layer-card-top">
                <span className="ide-layer-name">{layer.name}</span>
                <code className="ide-layer-path">{layer.paths}</code>
              </div>
              <p className="ide-layer-role">{layer.role}</p>
            </div>
          ))}
        </div>
      </div>

      <div className="ide-topo-edges-box">
        <div className="ide-box-head">
          <div>
            <h3 className="ide-box-title">Allowed vs Blocked Graph Transitions</h3>
            <p className="ide-box-sub">
              Click any blocked transition to open its v2 Cypher gate YAML
            </p>
          </div>
        </div>

        <div className="ide-edge-list">
          {pack.edges.map((edge, idx) =>
            edge.allow ? (
              <div key={idx} className="ide-edge-item is-allowed">
                <div className="ide-edge-flow">
                  <strong>{edge.from}</strong>
                  <span className="ide-edge-arrow">→</span>
                  <span>{edge.to}</span>
                </div>
                <code className="ide-edge-badge is-ok">{edge.code}</code>
              </div>
            ) : (
              <button
                key={idx}
                type="button"
                className="ide-edge-item is-blocked"
                onClick={() => edge.gateId && onInspectGate(edge.gateId)}
              >
                <div className="ide-edge-flow">
                  <strong>{edge.from}</strong>
                  <span className="ide-edge-arrow">↛</span>
                  <span>{edge.to}</span>
                </div>
                <code className="ide-edge-badge is-bad">BLOCKED · {edge.code} →</code>
              </button>
            ),
          )}
        </div>
      </div>
    </div>

    <div className="ide-gates-table-box">
      <div className="ide-box-head">
        <h3 className="ide-box-title">Included Semantic Invariant Gates ({gates.length})</h3>
        <span className="ide-box-sub">
          Click any row to inspect its v2 Node-First YAML &amp; Cypher query
        </span>
      </div>
      <div className="table-surface">
        <table className="bench-table">
          <thead>
            <tr>
              <th>Gate ID &amp; Name</th>
              <th>FOIM Coordinates</th>
              <th>Forbidden Graph Pattern</th>
              <th>Severity</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            {gates.map((g) => (
              <tr key={g.id}>
                <td>
                  <code>{g.id}</code>
                  <br />
                  <strong>{g.name}</strong>
                </td>
                <td>{g.coords}</td>
                <td>
                  <code>{g.pattern}</code>
                </td>
                <td className="cell-num">
                  <code className={`badge ${g.severity === 'Error' ? 'badge--bad' : 'badge--accent'}`}>
                    {g.severity}
                  </code>
                </td>
                <td className="cell-num">
                  <button
                    type="button"
                    className="ide-mini-btn"
                    onClick={() => onInspectGate(g.id)}
                  >
                    Open YAML →
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  </div>
);

export const ArchitecturesPage: React.FC = () => {
  const [selectedSlug, setSelectedSlug] = useState<string>('clean-architecture');
  const [activeTab, setActiveTab] = useState<EditorTabKey>('overview');
  const [activeStack, setActiveStack] = useState<StackKey>('agnostic');
  const [selectedGateId, setSelectedGateId] = useState<string | null>(null);
  const [bottomTab, setBottomTab] = useState<BottomTabKey>('problems');
  const [bottomOpen, setBottomOpen] = useState<boolean>(true);
  const [copiedFeedback, setCopiedFeedback] = useState<string | null>(null);

  const pack = useMemo(
    () => ARCHITECTURE_PACKS.find((p) => p.slug === selectedSlug) || ARCHITECTURE_PACKS[0],
    [selectedSlug],
  );

  const stackPaths = STACK_CONFIGS[activeStack];
  const layers = useMemo(() => pack.layers(stackPaths), [pack, stackPaths]);
  const gates = useMemo(() => pack.gates(stackPaths, activeStack), [pack, stackPaths, activeStack]);

  const structuralPacks = useMemo(
    () => ARCHITECTURE_PACKS.filter((p) => p.category === 'structural'),
    [],
  );
  const runtimePacks = useMemo(
    () => ARCHITECTURE_PACKS.filter((p) => p.category === 'runtime'),
    [],
  );

  const displayedGates = useMemo(() => {
    if (!selectedGateId) return gates;
    const match = gates.filter((g) => g.id === selectedGateId);
    return match.length > 0 ? match : gates;
  }, [gates, selectedGateId]);

  const bundleYaml = useMemo(
    () => buildPackBundleYaml(pack, activeStack, displayedGates),
    [pack, activeStack, displayedGates],
  );

  const yamlLines = useMemo(() => bundleYaml.trimEnd().split('\n'), [bundleYaml]);

  function triggerToast(label: string): void {
    setCopiedFeedback(label);
    setTimeout(() => {
      setCopiedFeedback((prev) => (prev === label ? null : prev));
    }, 1800);
  }

  function handleSelectPack(slug: string): void {
    setSelectedSlug(slug);
    setSelectedGateId(null);
  }

  function handleInspectGate(gateId: string): void {
    setSelectedGateId(gateId);
    setActiveTab('yaml');
  }

  function handleCopyYaml(): void {
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(bundleYaml).then(() => triggerToast('Copied YAML'));
    }
  }

  function handleCopyPrompt(): void {
    const fullBundle = buildPackBundleYaml(pack, activeStack, gates);
    const promptText =
      `Inspect my workspace using CodeExplorer get_architecture_map, adapt the following '${pack.title}' (${activeStack}) Semantic Invariant Gate pack to match our actual folder and project paths, save the rules to .codeexplorer/gates/${pack.slug}.yaml, run check_semantic_gates, and freeze existing baseline debt via ce gate ratchet --update:\n\n` +
      fullBundle;
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(promptText).then(() => triggerToast('Copied AI Prompt'));
    }
  }

  function handleDownloadYaml(): void {
    const fullBundle = buildPackBundleYaml(pack, activeStack, gates);
    const blob = new Blob([fullBundle], { type: 'text/yaml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${pack.slug}-${activeStack}.gates.yaml`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    triggerToast('Downloaded .yaml');
  }

  const cliCommand =
    activeStack === 'agnostic' ? 'ce gate check --scan' : `ce gate check -p ${activeStack} --scan`;

  return (
    <main id="main-content" className="arch-page">
      <section className="arch-top-header">
        <div className="wrap arch-top-bar">
          <div className="arch-top-copy">
            <div className="arch-eyebrow-row">
              <code className="arch-eyebrow-badge">ARCHITECTURE EXPLORER &amp; GATE PACKS</code>
              <span className="arch-eyebrow-meta">
                10 packs · {ARCHITECTURE_PACKS.reduce((acc, p) => acc + p.gates(stackPaths, activeStack).length, 0)} executable v2 Cypher invariants
              </span>
            </div>
            <h1 className="arch-main-title">Read the architecture. Understand how it breaks. Execute the rules.</h1>
          </div>

          <div className="arch-top-actions">
            <button type="button" className="btn" onClick={handleCopyPrompt}>
              {copiedFeedback === 'Copied AI Prompt' ? 'Prompt Copied!' : 'Copy AI Adapt Prompt'}
            </button>
            <button type="button" className="btn btn--primary" onClick={handleDownloadYaml}>
              {copiedFeedback === 'Downloaded .yaml' ? 'Downloaded!' : `Download ${pack.slug}.yaml`}
            </button>
          </div>
        </div>
      </section>

      <section className="arch-workbench-section" aria-label="Architecture IDE Workbench">
        <div className="wrap">
          <div className="ide-shell">
            {/* Top IDE Window Bar */}
            <div className="ide-titlebar">
              <div className="ide-titlebar-left">
                <span className="ide-window-dots" aria-hidden="true">
                  <span className="ide-dot ide-dot--1" />
                  <span className="ide-dot ide-dot--2" />
                  <span className="ide-dot ide-dot--3" />
                </span>
                <span className="ide-workspace-label">
                  code-explorer-architectures — <code>{pack.fileName}</code>
                </span>
              </div>

              <div className="ide-stack-bar" role="group" aria-label="Language stack preset">
                <span className="ide-stack-label">Stack paths:</span>
                {STACK_KEYS.map((sk) => (
                  <button
                    key={sk}
                    type="button"
                    className={`ide-stack-pill${activeStack === sk ? ' is-active' : ''}`}
                    aria-pressed={activeStack === sk}
                    onClick={() => setActiveStack(sk)}
                  >
                    {STACK_CONFIGS[sk].label}
                  </button>
                ))}
              </div>
            </div>

            <div className="ide-body">
              {/* Left Sidebar: VS Code Explorer Tree */}
              <aside className="ide-sidebar" aria-label="Architecture packs explorer">
                <div className="ide-sidebar-header">
                  <span>EXPLORER</span>
                  <code className="ide-sidebar-count">10 PACKS</code>
                </div>

                <div className="ide-tree">
                  <div className="ide-folder-group">
                    <div className="ide-folder-title">
                      <span className="ide-chevron" aria-hidden="true">▾</span>
                      <span>STRUCTURAL-STYLES</span>
                    </div>
                    <ul className="ide-file-list" role="listbox" aria-label="Structural architecture styles">
                      {structuralPacks.map((item) => {
                        const isSelected = item.slug === pack.slug;
                        const count = item.gates(stackPaths, activeStack).length;
                        return (
                          <li key={item.slug}>
                            <button
                              type="button"
                              role="option"
                              aria-selected={isSelected}
                              className={`ide-file-btn${isSelected ? ' is-selected' : ''}`}
                              onClick={() => handleSelectPack(item.slug)}
                            >
                              <span className="ide-file-icon ide-file-icon--arch" aria-hidden="true">◆</span>
                              <span className="ide-file-name">{item.shortName}</span>
                              <code className="ide-file-badge">{count}</code>
                            </button>
                          </li>
                        );
                      })}
                    </ul>
                  </div>

                  <div className="ide-folder-group">
                    <div className="ide-folder-title">
                      <span className="ide-chevron" aria-hidden="true">▾</span>
                      <span>RUNTIME-INVARIANTS</span>
                    </div>
                    <ul className="ide-file-list" role="listbox" aria-label="Runtime architectural invariants">
                      {runtimePacks.map((item) => {
                        const isSelected = item.slug === pack.slug;
                        const count = item.gates(stackPaths, activeStack).length;
                        return (
                          <li key={item.slug}>
                            <button
                              type="button"
                              role="option"
                              aria-selected={isSelected}
                              className={`ide-file-btn${isSelected ? ' is-selected' : ''}`}
                              onClick={() => handleSelectPack(item.slug)}
                            >
                              <span className="ide-file-icon ide-file-icon--runtime" aria-hidden="true">⚡</span>
                              <span className="ide-file-name">{item.shortName}</span>
                              <code className="ide-file-badge">{count}</code>
                            </button>
                          </li>
                        );
                      })}
                    </ul>
                  </div>
                </div>

                <div className="ide-sidebar-foot">
                  <div className="ide-sidebar-foot-label">Graph Layers Active</div>
                  <div className="ide-layer-pills">
                    {pack.graphLayersUsed.map((layer) => (
                      <code key={layer} className="ide-layer-tag">
                        {layer}
                      </code>
                    ))}
                  </div>
                </div>
              </aside>

              {/* Center Area: Editor Tabs + Content + Bottom Panel */}
              <div className="ide-main">
                {/* Editor Tab Strip */}
                <div className="ide-tabs-bar">
                  <div className="ide-tabs" role="tablist" aria-label="Architecture inspector views">
                    <button
                      type="button"
                      role="tab"
                      aria-selected={activeTab === 'overview'}
                      className={`ide-tab${activeTab === 'overview' ? ' is-active' : ''}`}
                      onClick={() => setActiveTab('overview')}
                    >
                      <span className="ide-tab-icon ide-tab-icon--md" aria-hidden="true">M↓</span>
                      <span>OVERVIEW.md</span>
                    </button>
                    <button
                      type="button"
                      role="tab"
                      aria-selected={activeTab === 'topology'}
                      className={`ide-tab${activeTab === 'topology' ? ' is-active' : ''}`}
                      onClick={() => setActiveTab('topology')}
                    >
                      <span className="ide-tab-icon ide-tab-icon--graph" aria-hidden="true">◈</span>
                      <span>TOPOLOGY.graph</span>
                    </button>
                    <button
                      type="button"
                      role="tab"
                      aria-selected={activeTab === 'yaml'}
                      className={`ide-tab${activeTab === 'yaml' ? ' is-active' : ''}`}
                      onClick={() => setActiveTab('yaml')}
                    >
                      <span className="ide-tab-icon ide-tab-icon--yaml" aria-hidden="true">{}</span>
                      <span>{pack.slug}.gates.yaml</span>
                      <code className="ide-tab-count">{gates.length}</code>
                    </button>
                  </div>

                  <div className="ide-tab-actions">
                    <button
                      type="button"
                      className="ide-mini-btn"
                      onClick={handleCopyYaml}
                      title="Copy YAML gate pack to clipboard"
                    >
                      {copiedFeedback === 'Copied YAML' ? 'Copied!' : 'Copy YAML'}
                    </button>
                  </div>
                </div>

                {/* Breadcrumbs Bar */}
                <div className="ide-breadcrumbs" aria-label="Breadcrumb">
                  <span>architectures</span>
                  <span className="ide-crumb-sep">/</span>
                  <span>{pack.category === 'structural' ? 'structural-styles' : 'runtime-invariants'}</span>
                  <span className="ide-crumb-sep">/</span>
                  <strong className="ide-crumb-current">{pack.slug}</strong>
                  <span className="ide-crumb-sep">/</span>
                  <code>
                    {activeTab === 'overview'
                      ? 'OVERVIEW.md'
                      : activeTab === 'topology'
                        ? 'TOPOLOGY.graph'
                        : `${pack.slug}.gates.yaml`}
                  </code>
                </div>

                {/* Editor Stage */}
                <div className="ide-editor-stage">
                  {activeTab === 'overview' && (
                    <OverviewTabContent
                      pack={pack}
                      gatesCount={gates.length}
                      stackLabel={stackPaths.label}
                      onOpenTopology={() => setActiveTab('topology')}
                      onOpenAllYaml={() => {
                        setSelectedGateId(null);
                        setActiveTab('yaml');
                      }}
                    />
                  )}

                  {activeTab === 'topology' && (
                    <TopologyTabContent
                      pack={pack}
                      layers={layers}
                      gates={gates}
                      stackLabel={stackPaths.label}
                      onInspectGate={handleInspectGate}
                    />
                  )}

                  {activeTab === 'yaml' && (
                    <div className="ide-yaml-view">
                      <div className="ide-yaml-toolbar">
                        <div className="ide-gate-filter-pills">
                          <button
                            type="button"
                            className={`ide-stack-pill${selectedGateId === null ? ' is-active' : ''}`}
                            onClick={() => setSelectedGateId(null)}
                          >
                            All Gates ({gates.length})
                          </button>
                          {gates.map((g) => (
                            <button
                              key={g.id}
                              type="button"
                              className={`ide-stack-pill${selectedGateId === g.id ? ' is-active' : ''}`}
                              onClick={() => setSelectedGateId(g.id)}
                            >
                              {g.id}
                            </button>
                          ))}
                        </div>

                        <div className="ide-yaml-actions">
                          <button type="button" className="ide-mini-btn" onClick={handleCopyYaml}>
                            {copiedFeedback === 'Copied YAML' ? 'Copied!' : 'Copy YAML'}
                          </button>
                          <button type="button" className="ide-mini-btn" onClick={handleDownloadYaml}>
                            Download .yaml
                          </button>
                        </div>
                      </div>

                      <div className="ide-code-editor" role="region" aria-label="Gate YAML source">
                        <div className="ide-line-gutter" aria-hidden="true">
                          {yamlLines.map((_, idx) => (
                            <span key={idx}>{idx + 1}</span>
                          ))}
                        </div>
                        <pre className="ide-yaml-pre">
                          <code>{bundleYaml}</code>
                        </pre>
                      </div>
                    </div>
                  )}
                </div>

                {/* Bottom Panel: VS Code PROBLEMS & TERMINAL */}
                <div className={`ide-bottom-panel${bottomOpen ? ' is-open' : ' is-collapsed'}`}>
                  <div className="ide-bottom-bar">
                    <div className="ide-bottom-tabs" role="tablist" aria-label="Bottom panel views">
                      <button
                        type="button"
                        role="tab"
                        aria-selected={bottomTab === 'problems'}
                        className={`ide-bottom-tab${bottomTab === 'problems' ? ' is-active' : ''}`}
                        onClick={() => {
                          setBottomTab('problems');
                          setBottomOpen(true);
                        }}
                      >
                        <span>PROBLEMS</span>
                        <code className="ide-problem-count">{gates.length}</code>
                      </button>
                      <button
                        type="button"
                        role="tab"
                        aria-selected={bottomTab === 'terminal'}
                        className={`ide-bottom-tab${bottomTab === 'terminal' ? ' is-active' : ''}`}
                        onClick={() => {
                          setBottomTab('terminal');
                          setBottomOpen(true);
                        }}
                      >
                        <span>TERMINAL — ce gate check</span>
                      </button>
                    </div>

                    <button
                      type="button"
                      className="ide-bottom-toggle"
                      onClick={() => setBottomOpen((prev) => !prev)}
                      aria-label={bottomOpen ? 'Collapse bottom panel' : 'Expand bottom panel'}
                    >
                      {bottomOpen ? '▾ Hide Panel' : '▴ Show Panel'}
                    </button>
                  </div>

                  {bottomOpen && (
                    <div className="ide-bottom-body">
                      {bottomTab === 'problems' ? (
                        <ul className="ide-problems-list" aria-label="Simulated gate violations">
                          {gates.map((g) => (
                            <li key={g.id} className="ide-problem-row">
                              <button
                                type="button"
                                className="ide-problem-btn"
                                onClick={() => handleInspectGate(g.id)}
                              >
                                <span
                                  className={`ide-problem-sev ${
                                    g.severity === 'Error' ? 'is-err' : 'is-warn'
                                  }`}
                                >
                                  {g.severity === 'Error' ? '✕' : '⚠'}
                                </span>
                                <code className="ide-problem-id">{g.id}</code>
                                <span className="ide-problem-msg">
                                  <strong>{g.sampleViolation.symbol}</strong> →{' '}
                                  <code>{g.sampleViolation.target}</code> — {g.sampleViolation.detail}
                                </span>
                                <code className="ide-problem-loc">
                                  {g.sampleViolation.file}:{g.sampleViolation.line}
                                </code>
                              </button>
                            </li>
                          ))}
                        </ul>
                      ) : (
                        <pre className="ide-terminal-pre">
                          <code>
                            {`$ ${cliCommand}\n`}
                            {`--- Semantic Invariant Gates Evaluation (${pack.title}) ---\n`}
                            {gates
                              .map(
                                (g) =>
                                  `[FAIL] ${g.id.padEnd(14)} ${g.severity.padEnd(8)} ${g.sampleViolation.file}:${g.sampleViolation.line}  ${g.sampleViolation.symbol} -> ${g.sampleViolation.target}`,
                              )
                              .join('\n')}
                            {`\n\n# Freeze legacy brownfield violations as a SHA-256 baseline ratchet so CI blocks only net-new leaks:\n`}
                            {`$ ce gate ratchet --update\n`}
                            {`✓ Recorded ${gates.length} baseline fingerprints in .codeexplorer/baselines/. Subsequent 'ce gate check' exits 0.`}
                          </code>
                        </pre>
                      )}
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* VS Code Status Bar */}
            <div className="ide-statusbar">
              <div className="ide-statusbar-left">
                <span className="ide-status-remote">⇄ CodeExplorer Graph Engine</span>
                <span>main*</span>
                <span>
                  ✗ {gates.filter((g) => g.severity === 'Error').length} Errors · ⚠{' '}
                  {gates.filter((g) => g.severity === 'Warning').length} Warnings
                </span>
              </div>
              <div className="ide-statusbar-right">
                <span>Stack: {stackPaths.label}</span>
                <span>OpenCypher v2 Node-First</span>
                <span>SQLite .codeexplorer/graph.db</span>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
};
