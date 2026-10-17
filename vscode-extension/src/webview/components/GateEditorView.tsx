import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import {
  GateItemDto,
  GateSuiteSummaryDto,
  SaveGateResponseDto,
  TestGateResponseDto,
  GateViolationDto,
} from '../../../../proto/types';
import { getVsCodeApi } from '../vscodeApi';
import { CypherEditor } from '../domain/components/CypherEditor';
import { ShieldIcon, EditIcon, CheckIcon, RotateCcwIcon, PlusIcon } from './Icons';
import { useServerTransport, webviewTransport } from '../services/webviewTransport';

export interface GateEditorViewProps {
  gateId?: string | null;
  initialGate?: GateItemDto | null;
  serverHttpUrl?: string;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSaveSuccess?: (savedGate: GateItemDto) => void;
}

const CATEGORIES = [
  'Architectural',
  'Security',
  'Reliability',
  'Performance',
  'Agility',
  'Compliance',
] as const;

const SCOPES = [
  'CrossService',
  'Project',
  'Module',
  'Type',
  'Function',
] as const;

const DIMENSIONS = [
  'Boundaries',
  'Dependencies',
  'Lifecycle',
  'Persistence',
  'Consistency',
  'Rules',
] as const;

const PLATFORMS = [
  'agnostic',
  'dotnet',
  'typescript',
  'python',
  'go',
  'java',
  'kotlin',
] as const;

const SEVERITIES = [
  'Error',
  'Warning',
  'Info',
] as const;

const STARTER_TEMPLATE = {
  id: 'SIG-CUSTOM-001',
  name: 'Custom Architectural Invariant',
  category: 'Architectural',
  scope: 'Module',
  dimension: 'Boundaries',
  platform: 'agnostic',
  severity: 'Error',
  description: 'Ensure architectural boundaries are respected.',
  remediation: 'Refactor violating components to decouple module dependencies.',
  query:
    `MATCH (caller:Function)-[:CALLS]->(callee:Function)\n` +
    `WHERE callee.name = 'ForbiddenMethod'\n` +
    `  AND NOT coalesce(caller.is_test, false)\n` +
    `RETURN \n` +
    `  coalesce(caller.path, caller.file_path) AS file,\n` +
    `  coalesce(caller.start_line, 1) AS line,\n` +
    `  caller.name AS symbol,\n` +
    `  callee.name AS target,\n` +
    `  'Calling ' + callee.name + ' is prohibited across module boundaries' AS message`,
  ratchetEnabled: true,
  baselinePath: '',
};

const cardStyle: React.CSSProperties = {
  backgroundColor: 'var(--vscode-sideBar-background, #252526)',
  border: '1px solid var(--vscode-panel-border, #333)',
  borderRadius: '6px',
  padding: '20px',
  marginBottom: '20px',
};

const labelStyle: React.CSSProperties = {
  display: 'block',
  fontSize: '0.85rem',
  fontWeight: 600,
  marginBottom: '6px',
  color: 'var(--vscode-foreground, #cccccc)',
};

const inputStyle: React.CSSProperties = {
  width: '100%',
  padding: '8px 10px',
  backgroundColor: 'var(--vscode-input-background, #3c3c3c)',
  color: 'var(--vscode-input-foreground, #fff)',
  border: '1px solid var(--vscode-input-border, #555)',
  borderRadius: '4px',
  fontSize: '0.88rem',
  boxSizing: 'border-box',
};

const selectStyle: React.CSSProperties = {
  width: '100%',
  padding: '8px 10px',
  backgroundColor: 'var(--vscode-dropdown-background, #3c3c3c)',
  color: 'var(--vscode-dropdown-foreground, #fff)',
  border: '1px solid var(--vscode-dropdown-border, #555)',
  borderRadius: '4px',
  fontSize: '0.88rem',
  boxSizing: 'border-box',
};

export const GateEditorView: React.FC<GateEditorViewProps> = ({
  gateId,
  initialGate,
  serverHttpUrl,
  onOpenFile,
  onSaveSuccess,
}) => {
  const [gatesList, setGatesList] = useState<GateItemDto[]>([]);
  const [selectedGateId, setSelectedGateId] = useState<string>(gateId || '');

  // Form Fields
  const [id, setId] = useState<string>(STARTER_TEMPLATE.id);
  const [name, setName] = useState<string>(STARTER_TEMPLATE.name);
  const [category, setCategory] = useState<string>(STARTER_TEMPLATE.category);
  const [scope, setScope] = useState<string>(STARTER_TEMPLATE.scope);
  const [dimension, setDimension] = useState<string>(STARTER_TEMPLATE.dimension);
  const [platform, setPlatform] = useState<string>(STARTER_TEMPLATE.platform);
  const [severity, setSeverity] = useState<string>(STARTER_TEMPLATE.severity);
  const [description, setDescription] = useState<string>(STARTER_TEMPLATE.description);
  const [remediation, setRemediation] = useState<string>(STARTER_TEMPLATE.remediation);
  const [query, setQuery] = useState<string>(STARTER_TEMPLATE.query);
  const [ratchetEnabled, setRatchetEnabled] = useState<boolean>(STARTER_TEMPLATE.ratchetEnabled);
  const [baselinePath, setBaselinePath] = useState<string>(STARTER_TEMPLATE.baselinePath);
  const [gateSource, setGateSource] = useState<string | undefined>(undefined);

  // Status & Dry-Run State
  const [isTesting, setIsTesting] = useState<boolean>(false);
  const [testResult, setTestResult] = useState<TestGateResponseDto | null>(null);
  const [testError, setTestError] = useState<string | null>(null);

  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState<boolean>(false);
  const saveTimerRef = useRef<NodeJS.Timeout | null>(null);

  const { httpUrl: transportHttpUrl } = useServerTransport();
  const resolvedHttpUrl = serverHttpUrl || transportHttpUrl;

  const populateFormWithGate = useCallback((g: GateItemDto | null) => {
    if (g) {
      setId(g.id || '');
      setName(g.name || '');
      setCategory(g.category || 'Architectural');
      setScope(g.scope || 'Module');
      setDimension(g.dimension || 'Boundaries');
      setPlatform(g.platform || 'agnostic');
      setSeverity(g.severity || 'Error');
      setDescription(g.description || '');
      setRemediation(g.remediation || '');
      setQuery(g.query || '');
      setRatchetEnabled(g.ratchetEnabled ?? true);
      setBaselinePath(g.baselinePath || '');
      setGateSource(g.source);
    } else {
      setId(STARTER_TEMPLATE.id);
      setName(STARTER_TEMPLATE.name);
      setCategory(STARTER_TEMPLATE.category);
      setScope(STARTER_TEMPLATE.scope);
      setDimension(STARTER_TEMPLATE.dimension);
      setPlatform(STARTER_TEMPLATE.platform);
      setSeverity(STARTER_TEMPLATE.severity);
      setDescription(STARTER_TEMPLATE.description);
      setRemediation(STARTER_TEMPLATE.remediation);
      setQuery(STARTER_TEMPLATE.query);
      setRatchetEnabled(STARTER_TEMPLATE.ratchetEnabled);
      setBaselinePath(STARTER_TEMPLATE.baselinePath);
      setGateSource(undefined);
    }
    setTestResult(null);
    setTestError(null);
    setSaveError(null);
    setSaveSuccess(false);
  }, []);

  // Fetch gates list on mount or server URL change
  const fetchGates = useCallback(async () => {
    const vscode = getVsCodeApi();
    if (vscode) {
      vscode.postMessage({ type: 'GET_GATES' });
    }
    if (!resolvedHttpUrl) return;
    try {
      const data = await webviewTransport.fetchJson<GateSuiteSummaryDto>('/api/gates');
      if (data?.gates) {
        setGatesList(data.gates);
      }
    } catch {
      // Backend might still be warming up
    }
  }, [resolvedHttpUrl]);

  useEffect(() => {
    fetchGates();
  }, [fetchGates]);

  // Synchronize when gateId prop changes
  useEffect(() => {
    const targetId = gateId || '';
    setSelectedGateId(targetId);

    if (initialGate) {
      populateFormWithGate(initialGate);
      return;
    }

    if (!targetId) {
      populateFormWithGate(null);
      return;
    }

    if (gatesList.length > 0) {
      const found = gatesList.find((g) => g.id.toLowerCase() === targetId.toLowerCase());
      if (found) {
        populateFormWithGate(found);
      } else {
        populateFormWithGate(null);
        setId(targetId);
      }
    }
  }, [gateId, initialGate, gatesList, populateFormWithGate]);

  // Listen for extension / transport messages
  useEffect(() => {
    const handleMessage = (event: MessageEvent) => {
      const msg = event.data;
      if (!msg) return;

      if (msg.type === 'GATES_UPDATED' || msg.type === 'INITIAL_GATES_STATE') {
        const nextGates: GateItemDto[] | undefined = msg.suite?.gates || msg.gates;
        if (nextGates) {
          setGatesList(nextGates);
          if (selectedGateId) {
            const found = nextGates.find((g) => g.id.toLowerCase() === selectedGateId.toLowerCase());
            if (found && !gateSource) {
              populateFormWithGate(found);
            }
          }
        }
      } else if (msg.type === 'GATE_TEST_RESULT') {
        setIsTesting(false);
        if (msg.success && msg.result) {
          setTestResult(msg.result);
          setTestError(null);
        } else {
          setTestError(msg.error || 'Query test failed');
          setTestResult(null);
        }
      } else if (msg.type === 'GATE_SAVED') {
        setIsSaving(false);
        if (msg.success && msg.result) {
          setSaveError(null);
          setSaveSuccess(true);
          if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
          saveTimerRef.current = setTimeout(() => setSaveSuccess(false), 4000);

          if (msg.result.gate) {
            const saved: GateItemDto = msg.result.gate;
            setGateSource(saved.source);
            setSelectedGateId(saved.id);
            setGatesList((prev) => {
              const idx = prev.findIndex((x) => x.id.toLowerCase() === saved.id.toLowerCase());
              if (idx >= 0) {
                const next = [...prev];
                next[idx] = saved;
                return next;
              }
              return [...prev, saved];
            });
            onSaveSuccess?.(saved);
          }
        } else {
          setSaveError(msg.error || 'Failed to save gate');
        }
      }
    };

    window.addEventListener('message', handleMessage);
    return () => {
      window.removeEventListener('message', handleMessage);
      if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
    };
  }, [selectedGateId, gateSource, populateFormWithGate, onSaveSuccess]);

  // Handler for gate selector change
  const handleSelectGate = useCallback((targetId: string) => {
    setSelectedGateId(targetId);
    if (!targetId) {
      populateFormWithGate(null);
      return;
    }
    const found = gatesList.find((g) => g.id.toLowerCase() === targetId.toLowerCase());
    if (found) {
      populateFormWithGate(found);
    }
  }, [gatesList, populateFormWithGate]);

  // Execute Dry-Run Test
  const handleTestQuery = useCallback(async () => {
    const trimmedQuery = query.trim();
    if (!trimmedQuery) {
      setTestError('Query must not be empty.');
      return;
    }

    setIsTesting(true);
    setTestError(null);
    setTestResult(null);

    const vscode = getVsCodeApi();
    if (vscode) {
      vscode.postMessage({
        type: 'TEST_GATE',
        query: trimmedQuery,
        gateId: id.trim(),
      });
      return;
    }

    if (!resolvedHttpUrl) {
      setTestError('Backend server URL not available.');
      setIsTesting(false);
      return;
    }

    try {
      const data = await webviewTransport.fetchJson<TestGateResponseDto>('/api/gates/test', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          query: trimmedQuery,
          gateId: id.trim(),
        }),
      });

      if (data.success) {
        setTestResult(data);
        setTestError(null);
      } else {
        setTestError(data.error || 'Query test failed');
      }
    } catch (err: any) {
      setTestError(err?.message || 'Failed to execute query test');
    } finally {
      setIsTesting(false);
    }
  }, [query, id, resolvedHttpUrl]);

  // Save Gate Definition
  const handleSave = useCallback(async () => {
    const trimmedId = id.trim();
    if (!trimmedId) {
      setSaveError('Gate ID is required.');
      return;
    }
    const trimmedQuery = query.trim();
    if (!trimmedQuery) {
      setSaveError('Gate Cypher query is required.');
      return;
    }

    setIsSaving(true);
    setSaveError(null);

    const payloadGate: Partial<GateItemDto> = {
      id: trimmedId,
      name: name.trim() || trimmedId,
      category,
      scope,
      dimension,
      platform: platform.trim() || 'agnostic',
      severity,
      description: description.trim() || undefined,
      remediation: remediation.trim() || undefined,
      query: trimmedQuery,
      ratchetEnabled,
      baselinePath: baselinePath.trim() || undefined,
      source: gateSource,
    };

    const targetPath = gateSource && gateSource !== 'BuiltIn' ? gateSource : undefined;

    const vscode = getVsCodeApi();
    if (vscode) {
      vscode.postMessage({
        type: 'SAVE_GATE',
        gate: payloadGate,
        targetPath,
      });
      return;
    }

    if (!resolvedHttpUrl) {
      setSaveError('Backend server URL not available.');
      setIsSaving(false);
      return;
    }

    try {
      const data = await webviewTransport.fetchJson<SaveGateResponseDto>('/api/gates/save', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          gate: payloadGate,
          targetPath,
        }),
      });

      if (data.success && data.gate) {
        setSaveError(null);
        setSaveSuccess(true);
        if (saveTimerRef.current) clearTimeout(saveTimerRef.current);
        saveTimerRef.current = setTimeout(() => setSaveSuccess(false), 4000);

        setGateSource(data.gate.source);
        setSelectedGateId(data.gate.id);
        setGatesList((prev) => {
          const idx = prev.findIndex((x) => x.id.toLowerCase() === data.gate!.id.toLowerCase());
          if (idx >= 0) {
            const next = [...prev];
            next[idx] = data.gate!;
            return next;
          }
          return [...prev, data.gate!];
        });
        onSaveSuccess?.(data.gate);
      } else {
        setSaveError(data.message || 'Failed to save gate');
      }
    } catch (err: any) {
      setSaveError(err?.message || 'Failed to save gate');
    } finally {
      setIsSaving(false);
    }
  }, [
    id,
    name,
    category,
    scope,
    dimension,
    platform,
    severity,
    description,
    remediation,
    query,
    ratchetEnabled,
    baselinePath,
    gateSource,
    resolvedHttpUrl,
    onSaveSuccess,
  ]);

  const handleOpenRawYaml = useCallback(() => {
    const filePath =
      gateSource && gateSource !== 'BuiltIn'
        ? gateSource
        : `.codeexplorer/gates/${id.toLowerCase()}.yaml`;

    const vscode = getVsCodeApi();
    if (vscode) {
      vscode.postMessage({
        type: 'OPEN_GATE_YAML',
        gateId: id,
        filePath,
      });
    } else if (onOpenFile) {
      onOpenFile(filePath, 1);
    }
  }, [gateSource, id, onOpenFile]);

  const isNewGate = !selectedGateId;

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        width: '100%',
        padding: '24px 32px',
        overflowY: 'auto',
        backgroundColor: 'var(--vscode-editor-background, #1e1e1e)',
        color: 'var(--vscode-editor-foreground, #cccccc)',
        fontFamily: 'var(--font-family)',
        boxSizing: 'border-box',
      }}
    >
      {/* Top Header Bar */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          borderBottom: '1px solid var(--vscode-panel-border, #333)',
          paddingBottom: '16px',
          marginBottom: '24px',
          gap: '16px',
          flexWrap: 'wrap',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: 34,
              height: 34,
              borderRadius: '8px',
              backgroundColor: 'rgba(56, 189, 248, 0.15)',
              color: '#38bdf8',
            }}
          >
            <ShieldIcon size={20} />
          </span>
          <div>
            <h2 style={{ fontSize: '1.35rem', fontWeight: 600, color: 'var(--vscode-foreground, #fff)', margin: 0 }}>
              🛡️ Semantic Invariant Gate Editor
            </h2>
            <p style={{ margin: '4px 0 0', fontSize: '0.86rem', opacity: 0.8 }}>
              Define architectural invariants, dependency boundaries, and technical debt ratchets using OpenCypher.
            </p>
          </div>
        </div>

        {/* Top Right Quick Selector & Actions */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <span style={{ fontSize: '0.82rem', opacity: 0.85, fontWeight: 500 }}>Active Gate:</span>
            <select
              value={selectedGateId}
              onChange={(e) => handleSelectGate(e.target.value)}
              style={{
                ...selectStyle,
                width: 'auto',
                minWidth: '220px',
                maxWidth: '360px',
                padding: '6px 10px',
                fontSize: '0.84rem',
              }}
            >
              <option value="">+ Create New Gate</option>
              {gatesList.map((g) => (
                <option key={g.id} value={g.id}>
                  {g.id} — {g.name}
                </option>
              ))}
            </select>
          </div>

          {!isNewGate && (
            <button
              type="button"
              onClick={handleOpenRawYaml}
              style={{
                padding: '6px 12px',
                backgroundColor: 'var(--vscode-button-secondaryBackground, #3a3d41)',
                color: 'var(--vscode-button-secondaryForeground, #ffffff)',
                border: 'none',
                borderRadius: '4px',
                cursor: 'pointer',
                fontWeight: 500,
                fontSize: '0.84rem',
                display: 'flex',
                alignItems: 'center',
                gap: '5px',
              }}
              title="Open underlying YAML file in VS Code editor"
            >
              <EditIcon size={12} />
              <span>Open YAML</span>
            </button>
          )}

          <button
            type="button"
            onClick={handleTestQuery}
            disabled={isTesting || !query.trim()}
            style={{
              padding: '6px 14px',
              backgroundColor: 'var(--vscode-button-secondaryBackground, #3a3d41)',
              color: 'var(--vscode-button-secondaryForeground, #ffffff)',
              border: 'none',
              borderRadius: '4px',
              cursor: isTesting || !query.trim() ? 'wait' : 'pointer',
              fontWeight: 500,
              fontSize: '0.84rem',
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
            }}
          >
            <RotateCcwIcon size={12} />
            <span>{isTesting ? 'Testing...' : 'Test Query'}</span>
          </button>

          <button
            type="button"
            onClick={handleSave}
            disabled={isSaving || !id.trim() || !query.trim()}
            style={{
              padding: '6px 16px',
              backgroundColor: 'var(--vscode-button-background, #0e639c)',
              color: 'var(--vscode-button-foreground, #ffffff)',
              border: 'none',
              borderRadius: '4px',
              cursor: isSaving || !id.trim() || !query.trim() ? 'not-allowed' : 'pointer',
              fontWeight: 600,
              fontSize: '0.84rem',
              opacity: isSaving || !id.trim() || !query.trim() ? 0.6 : 1,
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
            }}
          >
            <CheckIcon size={12} />
            <span>{isSaving ? 'Saving...' : 'Save Gate'}</span>
          </button>
        </div>
      </div>

      {/* Section 1: Identity & Classification */}
      <div style={cardStyle}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '14px' }}>
          <h3 style={{ fontSize: '0.98rem', fontWeight: 600, margin: 0, color: '#38bdf8' }}>
            1. Identity & Classification
          </h3>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span
              style={{
                fontSize: '11px',
                fontWeight: 700,
                padding: '2px 8px',
                borderRadius: '4px',
                backgroundColor:
                  severity === 'Error'
                    ? 'rgba(239, 68, 68, 0.2)'
                    : severity === 'Warning'
                    ? 'rgba(245, 158, 11, 0.2)'
                    : 'rgba(56, 189, 248, 0.2)',
                color:
                  severity === 'Error'
                    ? '#f87171'
                    : severity === 'Warning'
                    ? '#fbbf24'
                    : '#38bdf8',
                border: `1px solid ${
                  severity === 'Error'
                    ? 'rgba(239, 68, 68, 0.35)'
                    : severity === 'Warning'
                    ? 'rgba(245, 158, 11, 0.35)'
                    : 'rgba(56, 189, 248, 0.35)'
                }`,
              }}
            >
              {severity.toUpperCase()}
            </span>
            {gateSource && (
              <span
                style={{
                  fontSize: '11px',
                  color: '#94a3b8',
                  backgroundColor: 'rgba(255, 255, 255, 0.05)',
                  padding: '2px 8px',
                  borderRadius: '4px',
                  fontFamily: 'monospace',
                }}
                title={gateSource}
              >
                {gateSource.split(/[/\\]/).pop()}
              </span>
            )}
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '14px' }}>
          {/* Gate ID */}
          <div>
            <label style={labelStyle}>
              Gate ID <span style={{ color: '#ef4444' }}>*</span>
            </label>
            <input
              type="text"
              value={id}
              onChange={(e) => setId(e.target.value.toUpperCase())}
              placeholder="SIG-ARCH-001"
              style={{ ...inputStyle, fontFamily: 'monospace' }}
            />
          </div>

          {/* Human Name */}
          <div style={{ gridColumn: 'span 2' }}>
            <label style={labelStyle}>
              Gate Name <span style={{ color: '#ef4444' }}>*</span>
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. View Generator Independence and Decoupling"
              style={inputStyle}
            />
          </div>

          {/* Category */}
          <div>
            <label style={labelStyle}>Category</label>
            <select value={category} onChange={(e) => setCategory(e.target.value)} style={selectStyle}>
              {CATEGORIES.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </div>

          {/* Scope */}
          <div>
            <label style={labelStyle}>Scope</label>
            <select value={scope} onChange={(e) => setScope(e.target.value)} style={selectStyle}>
              {SCOPES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
          </div>

          {/* Dimension */}
          <div>
            <label style={labelStyle}>Dimension</label>
            <select value={dimension} onChange={(e) => setDimension(e.target.value)} style={selectStyle}>
              {DIMENSIONS.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </select>
          </div>

          {/* Platform */}
          <div>
            <label style={labelStyle}>Platform</label>
            <select value={platform} onChange={(e) => setPlatform(e.target.value)} style={selectStyle}>
              {PLATFORMS.map((p) => (
                <option key={p} value={p}>
                  {p}
                </option>
              ))}
            </select>
          </div>

          {/* Severity */}
          <div>
            <label style={labelStyle}>Severity</label>
            <select value={severity} onChange={(e) => setSeverity(e.target.value)} style={selectStyle}>
              {SEVERITIES.map((sev) => (
                <option key={sev} value={sev}>
                  {sev}
                </option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {/* Section 2: Description & Remediation */}
      <div style={cardStyle}>
        <h3 style={{ fontSize: '0.98rem', fontWeight: 600, margin: '0 0 14px', color: '#38bdf8' }}>
          2. Description & Remediation
        </h3>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '14px' }}>
          <div>
            <label style={labelStyle}>Description / Rule Intent</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Explain the architectural principle being enforced..."
              rows={3}
              style={{ ...inputStyle, resize: 'vertical', fontFamily: 'inherit', lineHeight: 1.4 }}
            />
          </div>
          <div>
            <label style={labelStyle}>Remediation Guidance</label>
            <textarea
              value={remediation}
              onChange={(e) => setRemediation(e.target.value)}
              placeholder="Instruct developers on how to resolve detected violations..."
              rows={3}
              style={{ ...inputStyle, resize: 'vertical', fontFamily: 'inherit', lineHeight: 1.4 }}
            />
          </div>
        </div>
      </div>

      {/* Section 3: OpenCypher Rule Query */}
      <div style={cardStyle}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '10px' }}>
          <div>
            <h3 style={{ fontSize: '0.98rem', fontWeight: 600, margin: 0, color: '#38bdf8' }}>
              3. OpenCypher Invariant Rule (Zero-Match Contract)
            </h3>
            <p style={{ margin: '3px 0 0', fontSize: '0.82rem', opacity: 0.8 }}>
              Query returns 0 rows if compliant; each returned row represents an invariant violation.
            </p>
          </div>
          <span style={{ fontSize: '0.8rem', color: '#94a3b8' }}>
            Expected columns: <code>file</code>, <code>line</code>, <code>symbol</code>, <code>target</code>, <code>message</code>
          </span>
        </div>

        <CypherEditor
          value={query}
          onChange={setQuery}
          minHeight={220}
          maxHeight={380}
          onRunTest={handleTestQuery}
        />

        {/* Ratchet Baseline Configuration */}
        <div
          style={{
            marginTop: '16px',
            padding: '12px 16px',
            backgroundColor: 'rgba(0, 0, 0, 0.2)',
            borderRadius: '6px',
            border: '1px solid rgba(255, 255, 255, 0.08)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: '14px',
            flexWrap: 'wrap',
          }}
        >
          <label
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              cursor: 'pointer',
              fontSize: '0.86rem',
              color: '#e2e8f0',
              userSelect: 'none',
            }}
          >
            <input
              type="checkbox"
              checked={ratchetEnabled}
              onChange={(e) => setRatchetEnabled(e.target.checked)}
              style={{ cursor: 'pointer' }}
            />
            <span style={{ fontWeight: 600 }}>Enable Ratchet Baseline</span>
            <span style={{ fontSize: '0.8rem', color: '#94a3b8' }}>
              (Tolerates existing technical debt and strictly prevents new regressions)
            </span>
          </label>

          {ratchetEnabled && (
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flex: 1, minWidth: '260px' }}>
              <span style={{ fontSize: '0.82rem', color: '#94a3b8', whiteSpace: 'nowrap' }}>Baseline Path:</span>
              <input
                type="text"
                value={baselinePath}
                onChange={(e) => setBaselinePath(e.target.value)}
                placeholder={`baselines/${id.toLowerCase()}.json`}
                style={{ ...inputStyle, padding: '5px 8px', fontSize: '0.84rem', fontFamily: 'monospace' }}
              />
            </div>
          )}
        </div>
      </div>

      {/* Section 4: Test Results / Violations Preview */}
      <div style={cardStyle}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '12px', flexWrap: 'wrap', gap: '10px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <h3 style={{ fontSize: '0.98rem', fontWeight: 600, margin: 0, color: '#38bdf8' }}>
              4. Dry-Run Test Results
            </h3>
            <button
              type="button"
              onClick={handleTestQuery}
              disabled={isTesting || !query.trim()}
              style={{
                padding: '4px 12px',
                backgroundColor: 'var(--vscode-button-secondaryBackground, #3a3d41)',
                color: 'var(--vscode-button-secondaryForeground, #ffffff)',
                border: 'none',
                borderRadius: '4px',
                cursor: isTesting || !query.trim() ? 'wait' : 'pointer',
                fontWeight: 500,
                fontSize: '0.82rem',
                display: 'flex',
                alignItems: 'center',
                gap: '5px',
              }}
            >
              <RotateCcwIcon size={11} />
              <span>{isTesting ? 'Running...' : 'Run Test Query (Dry-Run)'}</span>
            </button>
          </div>

          {testResult && (
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <span
                style={{
                  fontSize: '0.82rem',
                  color: testResult.violationsCount > 0 ? '#f87171' : '#34d399',
                  fontWeight: 600,
                }}
              >
                ⚡ {testResult.durationMs}ms · {testResult.violationsCount} violation(s) found
              </span>
              {testResult.missingColumns && testResult.missingColumns.length > 0 && (
                <span
                  style={{
                    fontSize: '0.78rem',
                    color: '#fbbf24',
                    backgroundColor: 'rgba(245, 158, 11, 0.15)',
                    padding: '2px 8px',
                    borderRadius: '4px',
                    border: '1px solid rgba(245, 158, 11, 0.3)',
                  }}
                >
                  Missing columns: {testResult.missingColumns.join(', ')}
                </span>
              )}
            </div>
          )}
        </div>

        {/* Test Error Banner */}
        {testError && (
          <div
            style={{
              padding: '10px 14px',
              borderRadius: '6px',
              backgroundColor: 'rgba(239, 68, 68, 0.15)',
              border: '1px solid rgba(239, 68, 68, 0.35)',
              color: '#fca5a5',
              fontSize: '0.84rem',
              lineHeight: 1.4,
              fontFamily: 'monospace',
              marginBottom: '10px',
            }}
          >
            {testError}
          </div>
        )}

        {/* Compliant Banner */}
        {testResult && testResult.violations.length === 0 && (
          <div
            style={{
              padding: '10px 14px',
              borderRadius: '6px',
              backgroundColor: 'rgba(16, 185, 129, 0.12)',
              border: '1px solid rgba(16, 185, 129, 0.25)',
              color: '#6ee7b7',
              fontSize: '0.84rem',
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
            }}
          >
            <CheckIcon size={16} />
            <span>Zero violations found — invariant strictly holds across workspace graph!</span>
          </div>
        )}

        {/* Violations Table */}
        {testResult && testResult.violations.length > 0 && (
          <div
            style={{
              maxHeight: '260px',
              overflowY: 'auto',
              border: '1px solid rgba(255, 255, 255, 0.08)',
              borderRadius: '6px',
              backgroundColor: 'rgba(0, 0, 0, 0.25)',
            }}
          >
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.82rem', textAlign: 'left' }}>
              <thead>
                <tr
                  style={{
                    borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
                    backgroundColor: 'rgba(0, 0, 0, 0.3)',
                    color: '#94a3b8',
                  }}
                >
                  <th style={{ padding: '6px 10px', width: '36px' }}>#</th>
                  <th style={{ padding: '6px 10px' }}>File : Line</th>
                  <th style={{ padding: '6px 10px' }}>Symbol</th>
                  <th style={{ padding: '6px 10px' }}>Target</th>
                  <th style={{ padding: '6px 10px' }}>Message</th>
                </tr>
              </thead>
              <tbody>
                {testResult.violations.map((v: GateViolationDto, idx: number) => (
                  <tr
                    key={v.fingerprint || idx}
                    style={{
                      borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
                    }}
                  >
                    <td style={{ padding: '6px 10px', color: '#64748b' }}>{idx + 1}</td>
                    <td style={{ padding: '6px 10px', fontFamily: 'monospace' }}>
                      {v.file ? (
                        <span
                          onClick={() => onOpenFile?.(v.file!, v.line || 1)}
                          style={{
                            color: '#38bdf8',
                            cursor: onOpenFile ? 'pointer' : 'default',
                            textDecoration: onOpenFile ? 'underline' : 'none',
                          }}
                          title={v.file}
                        >
                          {v.file.split(/[/\\]/).pop()}:{v.line || 1}
                        </span>
                      ) : (
                        <span style={{ color: '#64748b' }}>-</span>
                      )}
                    </td>
                    <td style={{ padding: '6px 10px', color: '#e2e8f0', fontFamily: 'monospace' }}>
                      {v.symbol || '-'}
                    </td>
                    <td style={{ padding: '6px 10px', color: '#cbd5e1', fontFamily: 'monospace' }}>
                      {v.target || '-'}
                    </td>
                    <td style={{ padding: '6px 10px', color: '#f87171' }}>{v.message || '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!testResult && !testError && !isTesting && (
          <div style={{ fontSize: '0.82rem', color: '#94a3b8', fontStyle: 'italic' }}>
            Click &ldquo;Run Test Query&rdquo; or press Ctrl+Enter to test the invariant rule against the workspace graph.
          </div>
        )}
      </div>

      {/* Footer / Sticky Action Bar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginTop: 'auto',
          paddingTop: '16px',
          borderTop: '1px solid var(--vscode-panel-border, #333)',
          flexWrap: 'wrap',
          gap: '12px',
        }}
      >
        <div style={{ color: '#94a3b8', fontSize: '0.82rem' }}>
          Files saved directly to <code style={{ color: '#cbd5e1' }}>.codeexplorer/gates/</code>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          {saveError && (
            <span style={{ color: '#f87171', fontSize: '0.84rem', fontWeight: 500 }}>
              ✗ {saveError}
            </span>
          )}
          {saveSuccess && (
            <span style={{ color: '#34d399', fontSize: '0.86rem', fontWeight: 600 }}>
              ✓ Gate saved successfully!
            </span>
          )}

          <button
            type="button"
            onClick={handleTestQuery}
            disabled={isTesting || !query.trim()}
            style={{
              padding: '8px 16px',
              backgroundColor: 'var(--vscode-button-secondaryBackground, #3a3d41)',
              color: 'var(--vscode-button-secondaryForeground, #ffffff)',
              border: 'none',
              borderRadius: '4px',
              cursor: isTesting || !query.trim() ? 'wait' : 'pointer',
              fontWeight: 500,
              fontSize: '0.88rem',
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
            }}
          >
            <RotateCcwIcon size={13} />
            <span>{isTesting ? 'Testing...' : 'Test Query'}</span>
          </button>

          <button
            type="button"
            onClick={handleSave}
            disabled={isSaving || !id.trim() || !query.trim()}
            style={{
              padding: '8px 22px',
              backgroundColor: 'var(--vscode-button-background, #0e639c)',
              color: 'var(--vscode-button-foreground, #ffffff)',
              border: 'none',
              borderRadius: '4px',
              cursor: isSaving || !id.trim() || !query.trim() ? 'not-allowed' : 'pointer',
              fontWeight: 600,
              fontSize: '0.88rem',
              opacity: isSaving || !id.trim() || !query.trim() ? 0.6 : 1,
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
            }}
          >
            <CheckIcon size={14} />
            <span>{isSaving ? 'Saving...' : 'Save Gate'}</span>
          </button>
        </div>
      </div>
    </div>
  );
};
