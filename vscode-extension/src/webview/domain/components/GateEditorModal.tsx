import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  GateItemDto,
  SaveGateResponseDto,
  TestGateResponseDto,
  GateViolationDto,
} from '../../../../../proto/types';
import { getVsCodeApi } from '../../vscodeApi';
import { CypherEditor } from './CypherEditor';
import { ShieldIcon, EditIcon, CheckIcon, XIcon, RotateCcwIcon } from '../../components/Icons';

export interface GateEditorModalProps {
  isOpen: boolean;
  gate?: GateItemDto | null;
  serverHttpUrl?: string;
  onSaveSuccess: (savedGate: GateItemDto) => void;
  onClose: () => void;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
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

export const GateEditorModal: React.FC<GateEditorModalProps> = ({
  isOpen,
  gate,
  serverHttpUrl,
  onSaveSuccess,
  onClose,
  onOpenFile,
}) => {
  const isNew = !gate;

  const [id, setId] = useState<string>('');
  const [name, setName] = useState<string>('');
  const [category, setCategory] = useState<string>('Architectural');
  const [scope, setScope] = useState<string>('Module');
  const [dimension, setDimension] = useState<string>('Boundaries');
  const [platform, setPlatform] = useState<string>('agnostic');
  const [severity, setSeverity] = useState<string>('Error');
  const [description, setDescription] = useState<string>('');
  const [remediation, setRemediation] = useState<string>('');
  const [query, setQuery] = useState<string>('');
  const [ratchetEnabled, setRatchetEnabled] = useState<boolean>(true);
  const [baselinePath, setBaselinePath] = useState<string>('');

  const [isTesting, setIsTesting] = useState<boolean>(false);
  const [testResult, setTestResult] = useState<TestGateResponseDto | null>(null);
  const [testError, setTestError] = useState<string | null>(null);

  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const [activeWsUrl, setActiveWsUrl] = useState<string | undefined>(() => {
    return typeof window !== 'undefined' ? window.__CE_CONFIG__?.wsUrl : undefined;
  });

  useEffect(() => {
    const handleMsg = (e: MessageEvent) => {
      if ((e.data?.type === 'UPDATE_WS_URL' || e.data?.type === 'SERVER_CONFIG') && e.data?.wsUrl) {
        setActiveWsUrl(e.data.wsUrl);
      }
    };
    window.addEventListener('message', handleMsg);
    return () => window.removeEventListener('message', handleMsg);
  }, []);

  const resolvedHttpUrl = useMemo(() => {
    if (serverHttpUrl) return serverHttpUrl;
    const wsUrl = activeWsUrl || (typeof window !== 'undefined' ? window.__CE_CONFIG__?.wsUrl : undefined);
    if (wsUrl) {
      return wsUrl.replace(/^ws:\/\//, 'http://').replace(/^wss:\/\//, 'https://').replace(/\/ws$/, '');
    }
    return '';
  }, [serverHttpUrl, activeWsUrl]);

  // Reset or initialize state whenever modal opens or gate changes
  useEffect(() => {
    if (isOpen) {
      if (gate) {
        setId(gate.id || '');
        setName(gate.name || '');
        setCategory(gate.category || 'Architectural');
        setScope(gate.scope || 'Module');
        setDimension(gate.dimension || 'Boundaries');
        setPlatform(gate.platform || 'agnostic');
        setSeverity(gate.severity || 'Error');
        setDescription(gate.description || '');
        setRemediation(gate.remediation || '');
        setQuery(gate.query || '');
        setRatchetEnabled(gate.ratchetEnabled ?? true);
        setBaselinePath(gate.baselinePath || '');
      } else {
        // Default starter template for new gate
        setId('SIG-CUSTOM-001');
        setName('Custom Architectural Invariant');
        setCategory('Architectural');
        setScope('Module');
        setDimension('Boundaries');
        setPlatform('agnostic');
        setSeverity('Error');
        setDescription('Ensure architectural boundaries are respected.');
        setRemediation('Refactor violating components to decouple module dependencies.');
        setQuery(
          `MATCH (caller:Function)-[:CALLS]->(callee:Function)\n` +
          `WHERE callee.name = 'ForbiddenMethod'\n` +
          `  AND NOT coalesce(caller.is_test, false)\n` +
          `RETURN \n` +
          `  coalesce(caller.path, caller.file_path) AS file,\n` +
          `  coalesce(caller.start_line, 1) AS line,\n` +
          `  caller.name AS symbol,\n` +
          `  callee.name AS target,\n` +
          `  'Calling ' + callee.name + ' is prohibited across module boundaries' AS message`
        );
        setRatchetEnabled(true);
        setBaselinePath('');
      }
      setTestResult(null);
      setTestError(null);
      setSaveError(null);
      setIsTesting(false);
      setIsSaving(false);
    }
  }, [isOpen, gate]);

  // Listen for VS Code webview test and save response messages
  useEffect(() => {
    const handleMessage = (event: MessageEvent) => {
      const msg = event.data;
      if (msg?.type === 'GATE_TEST_RESULT') {
        setIsTesting(false);
        if (msg.success && msg.result) {
          setTestResult(msg.result);
          setTestError(null);
        } else {
          setTestError(msg.error || 'Query test failed');
          setTestResult(null);
        }
      } else if (msg?.type === 'GATE_SAVED') {
        setIsSaving(false);
        if (msg.success && msg.result) {
          setSaveError(null);
          if (msg.result.gate) {
            onSaveSuccess(msg.result.gate);
          }
          onClose();
        } else {
          setSaveError(msg.error || 'Failed to save gate');
        }
      }
    };

    window.addEventListener('message', handleMessage);
    return () => window.removeEventListener('message', handleMessage);
  }, [onSaveSuccess, onClose]);

  // Execute Query Test
  const handleTestQuery = useCallback(async () => {
    if (!query.trim()) {
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
        query,
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
      const res = await fetch(`${resolvedHttpUrl}/api/gates/test`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          query,
          gateId: id.trim(),
        }),
      });

      if (!res.ok) {
        throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      }

      const data = (await res.json()) as TestGateResponseDto;
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

  // Save Gate
  const handleSave = useCallback(async () => {
    const trimmedId = id.trim();
    if (!trimmedId) {
      setSaveError('Gate ID is required.');
      return;
    }
    if (!query.trim()) {
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
      query: query.trim(),
      ratchetEnabled,
      baselinePath: baselinePath.trim() || undefined,
      source: gate?.source,
    };

    const vscode = getVsCodeApi();
    if (vscode) {
      vscode.postMessage({
        type: 'SAVE_GATE',
        gate: payloadGate,
        targetPath: gate?.source && gate.source !== 'BuiltIn' ? gate.source : undefined,
      });
      return;
    }

    if (!resolvedHttpUrl) {
      setSaveError('Backend server URL not available.');
      setIsSaving(false);
      return;
    }

    try {
      const res = await fetch(`${resolvedHttpUrl}/api/gates/save`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          gate: payloadGate,
          targetPath: gate?.source && gate.source !== 'BuiltIn' ? gate.source : undefined,
        }),
      });

      if (!res.ok) {
        throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      }

      const data = (await res.json()) as SaveGateResponseDto;
      if (data.success && data.gate) {
        onSaveSuccess(data.gate);
        onClose();
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
    gate,
    resolvedHttpUrl,
    onSaveSuccess,
    onClose,
  ]);

  const handleOpenRawYaml = useCallback(() => {
    const filePath =
      gate?.source && gate.source !== 'BuiltIn'
        ? gate.source
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
  }, [gate, id, onOpenFile]);

  if (!isOpen) return null;

  return (
    <div
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.75)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 9999,
        backdropFilter: 'blur(4px)',
        padding: '16px',
        boxSizing: 'border-box',
      }}
      onClick={onClose}
    >
      <div
        style={{
          backgroundColor: 'var(--vscode-editor-background, #1e1e1e)',
          border: '1px solid var(--vscode-panel-border, #454545)',
          borderRadius: '10px',
          width: '880px',
          maxWidth: '96vw',
          maxHeight: '92vh',
          display: 'flex',
          flexDirection: 'column',
          color: 'var(--vscode-editor-foreground, #cccccc)',
          boxShadow: '0 12px 40px rgba(0, 0, 0, 0.65)',
          overflow: 'hidden',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Modal Header */}
        <div
          style={{
            padding: '14px 20px',
            borderBottom: '1px solid var(--vscode-panel-border, rgba(255, 255, 255, 0.1))',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            backgroundColor: 'rgba(0, 0, 0, 0.2)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <span
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: 28,
                height: 28,
                borderRadius: '6px',
                backgroundColor: 'rgba(56, 189, 248, 0.15)',
                color: '#38bdf8',
              }}
            >
              <ShieldIcon size={16} />
            </span>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <h3 style={{ margin: 0, fontSize: '15px', fontWeight: 600, color: '#f8fafc' }}>
                  {isNew ? 'Create Semantic Invariant Gate' : `Edit Gate: ${id}`}
                </h3>
                <span
                  style={{
                    fontSize: '10px',
                    fontWeight: 700,
                    padding: '2px 7px',
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
                {gate?.source && (
                  <span
                    style={{
                      fontSize: '10px',
                      color: '#94a3b8',
                      backgroundColor: 'rgba(255, 255, 255, 0.05)',
                      padding: '2px 6px',
                      borderRadius: '4px',
                      fontFamily: 'monospace',
                    }}
                    title={gate.source}
                  >
                    {gate.source.split('/').pop()}
                  </span>
                )}
              </div>
              <div style={{ fontSize: '11px', color: '#94a3b8', marginTop: '2px' }}>
                Define architectural invariants and technical debt gates using OpenCypher
              </div>
            </div>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            {!isNew && (
              <button
                type="button"
                onClick={handleOpenRawYaml}
                style={{
                  background: 'transparent',
                  border: '1px solid var(--vscode-panel-border, #454545)',
                  color: 'var(--vscode-editor-foreground, #cbd5e1)',
                  borderRadius: '4px',
                  padding: '4px 8px',
                  fontSize: '11px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '4px',
                  cursor: 'pointer',
                }}
                title="Open underlying YAML file in VS Code editor"
              >
                <EditIcon size={11} />
                <span>Open YAML</span>
              </button>
            )}
            <button
              type="button"
              onClick={onClose}
              style={{
                background: 'transparent',
                border: 'none',
                color: 'var(--vscode-editor-foreground, #94a3b8)',
                cursor: 'pointer',
                padding: '4px',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                borderRadius: '4px',
              }}
              title="Close editor (Esc)"
            >
              <XIcon size={16} />
            </button>
          </div>
        </div>

        {/* Modal Scrollable Content */}
        <div
          style={{
            flex: 1,
            overflowY: 'auto',
            padding: '18px 20px',
            display: 'flex',
            flexDirection: 'column',
            gap: '16px',
          }}
        >
          {/* Section 1: Identity & Categorization */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
              gap: '12px',
              padding: '12px 14px',
              backgroundColor: 'rgba(0, 0, 0, 0.15)',
              borderRadius: '8px',
              border: '1px solid rgba(255, 255, 255, 0.05)',
            }}
          >
            {/* Gate ID */}
            <div>
              <label
                style={{
                  display: 'block',
                  fontSize: '11px',
                  fontWeight: 600,
                  color: '#94a3b8',
                  marginBottom: '4px',
                }}
              >
                Gate ID <span style={{ color: '#ef4444' }}>*</span>
              </label>
              <input
                type="text"
                value={id}
                onChange={(e) => setId(e.target.value.toUpperCase())}
                placeholder="SIG-P-B-001"
                style={{
                  width: '100%',
                  padding: '6px 8px',
                  backgroundColor: 'var(--vscode-input-background, #252526)',
                  border: '1px solid var(--vscode-input-border, #3c3c3c)',
                  borderRadius: '4px',
                  color: 'var(--vscode-input-foreground, #ffffff)',
                  fontSize: '12px',
                  fontFamily: 'monospace',
                  boxSizing: 'border-box',
                }}
              />
            </div>

            {/* Name */}
            <div style={{ gridColumn: 'span 2' }}>
              <label
                style={{
                  display: 'block',
                  fontSize: '11px',
                  fontWeight: 600,
                  color: '#94a3b8',
                  marginBottom: '4px',
                }}
              >
                Human Name <span style={{ color: '#ef4444' }}>*</span>
              </label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. View Generator Independence and Decoupling"
                style={{
                  width: '100%',
                  padding: '6px 8px',
                  backgroundColor: 'var(--vscode-input-background, #252526)',
                  border: '1px solid var(--vscode-input-border, #3c3c3c)',
                  borderRadius: '4px',
                  color: 'var(--vscode-input-foreground, #ffffff)',
                  fontSize: '12px',
                  boxSizing: 'border-box',
                }}
              />
            </div>

            {/* Category */}
            <div>
              <label
                style={{
                  display: 'block',
                  fontSize: '11px',
                  fontWeight: 600,
                  color: '#94a3b8',
                  marginBottom: '4px',
                }}
              >
                Category
              </label>
              <select
                value={category}
                onChange={(e) => setCategory(e.target.value)}
                style={{
                  width: '100%',
                  padding: '6px 8px',
                  backgroundColor: 'var(--vscode-dropdown-background, #252526)',
                  border: '1px solid var(--vscode-dropdown-border, #3c3c3c)',
                  borderRadius: '4px',
                  color: 'var(--vscode-dropdown-foreground, #ffffff)',
                  fontSize: '12px',
                  boxSizing: 'border-box',
                }}
              >
                {CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </div>

            {/* Scope */}
            <div>
              <label
                style={{
                  display: 'block',
                  fontSize: '11px',
                  fontWeight: 600,
                  color: '#94a3b8',
                  marginBottom: '4px',
                }}
              >
                Scope
              </label>
              <select
                value={scope}
                onChange={(e) => setScope(e.target.value)}
                style={{
                  width: '100%',
                  padding: '6px 8px',
                  backgroundColor: 'var(--vscode-dropdown-background, #252526)',
                  border: '1px solid var(--vscode-dropdown-border, #3c3c3c)',
                  borderRadius: '4px',
                  color: 'var(--vscode-dropdown-foreground, #ffffff)',
                  fontSize: '12px',
                  boxSizing: 'border-box',
                }}
              >
                {SCOPES.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </select>
            </div>

            {/* Dimension */}
            <div>
              <label
                style={{
                  display: 'block',
                  fontSize: '11px',
                  fontWeight: 600,
                  color: '#94a3b8',
                  marginBottom: '4px',
                }}
              >
                Dimension
              </label>
              <select
                value={dimension}
                onChange={(e) => setDimension(e.target.value)}
                style={{
                  width: '100%',
                  padding: '6px 8px',
                  backgroundColor: 'var(--vscode-dropdown-background, #252526)',
                  border: '1px solid var(--vscode-dropdown-border, #3c3c3c)',
                  borderRadius: '4px',
                  color: 'var(--vscode-dropdown-foreground, #ffffff)',
                  fontSize: '12px',
                  boxSizing: 'border-box',
                }}
              >
                {DIMENSIONS.map((d) => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </select>
            </div>

            {/* Platform */}
            <div>
              <label
                style={{
                  display: 'block',
                  fontSize: '11px',
                  fontWeight: 600,
                  color: '#94a3b8',
                  marginBottom: '4px',
                }}
              >
                Platform
              </label>
              <select
                value={platform}
                onChange={(e) => setPlatform(e.target.value)}
                style={{
                  width: '100%',
                  padding: '6px 8px',
                  backgroundColor: 'var(--vscode-dropdown-background, #252526)',
                  border: '1px solid var(--vscode-dropdown-border, #3c3c3c)',
                  borderRadius: '4px',
                  color: 'var(--vscode-dropdown-foreground, #ffffff)',
                  fontSize: '12px',
                  boxSizing: 'border-box',
                }}
              >
                {PLATFORMS.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
              </select>
            </div>

            {/* Severity */}
            <div>
              <label
                style={{
                  display: 'block',
                  fontSize: '11px',
                  fontWeight: 600,
                  color: '#94a3b8',
                  marginBottom: '4px',
                }}
              >
                Severity
              </label>
              <select
                value={severity}
                onChange={(e) => setSeverity(e.target.value)}
                style={{
                  width: '100%',
                  padding: '6px 8px',
                  backgroundColor: 'var(--vscode-dropdown-background, #252526)',
                  border: '1px solid var(--vscode-dropdown-border, #3c3c3c)',
                  borderRadius: '4px',
                  color: 'var(--vscode-dropdown-foreground, #ffffff)',
                  fontSize: '12px',
                  boxSizing: 'border-box',
                }}
              >
                {SEVERITIES.map((sev) => (
                  <option key={sev} value={sev}>
                    {sev}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Section 2: Rule Intent & Remediation */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
              gap: '12px',
            }}
          >
            <div>
              <label
                style={{
                  display: 'block',
                  fontSize: '11px',
                  fontWeight: 600,
                  color: '#94a3b8',
                  marginBottom: '4px',
                }}
              >
                Description / Rule Intent
              </label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Explain the architectural principle being enforced..."
                rows={2}
                style={{
                  width: '100%',
                  padding: '6px 8px',
                  backgroundColor: 'var(--vscode-input-background, #252526)',
                  border: '1px solid var(--vscode-input-border, #3c3c3c)',
                  borderRadius: '4px',
                  color: 'var(--vscode-input-foreground, #ffffff)',
                  fontSize: '12px',
                  resize: 'vertical',
                  boxSizing: 'border-box',
                  fontFamily: 'inherit',
                }}
              />
            </div>
            <div>
              <label
                style={{
                  display: 'block',
                  fontSize: '11px',
                  fontWeight: 600,
                  color: '#94a3b8',
                  marginBottom: '4px',
                }}
              >
                Remediation Guidance
              </label>
              <textarea
                value={remediation}
                onChange={(e) => setRemediation(e.target.value)}
                placeholder="Instruct developers on how to fix detected violations..."
                rows={2}
                style={{
                  width: '100%',
                  padding: '6px 8px',
                  backgroundColor: 'var(--vscode-input-background, #252526)',
                  border: '1px solid var(--vscode-input-border, #3c3c3c)',
                  borderRadius: '4px',
                  color: 'var(--vscode-input-foreground, #ffffff)',
                  fontSize: '12px',
                  resize: 'vertical',
                  boxSizing: 'border-box',
                  fontFamily: 'inherit',
                }}
              />
            </div>
          </div>

          {/* Section 3: OpenCypher Invariant Query */}
          <div>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '6px',
              }}
            >
              <label
                style={{
                  fontSize: '11px',
                  fontWeight: 600,
                  color: '#94a3b8',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
              >
                <span>OpenCypher Query (Zero-Match Contract)</span>
                <span style={{ color: '#ef4444' }}>*</span>
              </label>
              <span style={{ fontSize: '11px', color: '#64748b' }}>
                Returns 0 rows if compliant; returns violating elements otherwise
              </span>
            </div>

            <CypherEditor
              value={query}
              onChange={setQuery}
              minHeight={200}
              maxHeight={320}
              onRunTest={handleTestQuery}
            />
          </div>

          {/* Section 4: Ratchet Debt Settings */}
          <div
            style={{
              padding: '10px 14px',
              backgroundColor: 'rgba(0, 0, 0, 0.15)',
              borderRadius: '6px',
              border: '1px solid rgba(255, 255, 255, 0.05)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: '12px',
              flexWrap: 'wrap',
            }}
          >
            <label
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                cursor: 'pointer',
                fontSize: '12px',
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
              <span style={{ fontSize: '11px', color: '#94a3b8' }}>
                (Tolerates existing debt and strictly prevents new violations)
              </span>
            </label>

            {ratchetEnabled && (
              <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flex: 1, minWidth: '220px' }}>
                <span style={{ fontSize: '11px', color: '#94a3b8' }}>Baseline:</span>
                <input
                  type="text"
                  value={baselinePath}
                  onChange={(e) => setBaselinePath(e.target.value)}
                  placeholder={`baselines/${id.toLowerCase()}.json`}
                  style={{
                    flex: 1,
                    padding: '4px 8px',
                    backgroundColor: 'var(--vscode-input-background, #252526)',
                    border: '1px solid var(--vscode-input-border, #3c3c3c)',
                    borderRadius: '4px',
                    color: 'var(--vscode-input-foreground, #ffffff)',
                    fontSize: '11px',
                    fontFamily: 'monospace',
                  }}
                />
              </div>
            )}
          </div>

          {/* Section 5: Query Test Execution & Live Violation Preview */}
          <div
            style={{
              borderRadius: '8px',
              border: '1px solid rgba(255, 255, 255, 0.08)',
              backgroundColor: 'rgba(15, 23, 42, 0.45)',
              padding: '12px 14px',
              display: 'flex',
              flexDirection: 'column',
              gap: '10px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '8px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <button
                  type="button"
                  onClick={handleTestQuery}
                  disabled={isTesting || !query.trim()}
                  style={{
                    backgroundColor: 'var(--vscode-button-secondaryBackground, #3a3d41)',
                    color: 'var(--vscode-button-secondaryForeground, #ffffff)',
                    border: 'none',
                    borderRadius: '4px',
                    padding: '5px 12px',
                    fontSize: '11.5px',
                    fontWeight: 600,
                    cursor: isTesting ? 'wait' : 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '6px',
                  }}
                  title="Execute Cypher query against current graph (Ctrl+Enter)"
                >
                  <RotateCcwIcon size={12} />
                  <span>{isTesting ? 'Testing Query...' : 'Test Query (Dry-Run)'}</span>
                </button>
                {testResult && (
                  <span
                    style={{
                      fontSize: '11px',
                      color: testResult.violationsCount > 0 ? '#f87171' : '#34d399',
                      fontWeight: 600,
                    }}
                  >
                    ⚡ {testResult.durationMs}ms · {testResult.violationsCount} violation(s) found
                  </span>
                )}
              </div>

              {testResult?.missingColumns && testResult.missingColumns.length > 0 && (
                <span
                  style={{
                    fontSize: '11px',
                    color: '#fbbf24',
                    backgroundColor: 'rgba(245, 158, 11, 0.14)',
                    padding: '2px 8px',
                    borderRadius: '4px',
                    border: '1px solid rgba(245, 158, 11, 0.3)',
                  }}
                >
                  Missing columns: {testResult.missingColumns.join(', ')}
                </span>
              )}
            </div>

            {/* Test Error Banner */}
            {testError && (
              <div
                style={{
                  padding: '8px 12px',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(239, 68, 68, 0.15)',
                  border: '1px solid rgba(239, 68, 68, 0.35)',
                  color: '#fca5a5',
                  fontSize: '11.5px',
                  lineHeight: 1.4,
                  fontFamily: 'monospace',
                }}
              >
                {testError}
              </div>
            )}

            {/* Test Results Table */}
            {testResult && testResult.violations.length > 0 && (
              <div
                style={{
                  maxHeight: '180px',
                  overflowY: 'auto',
                  border: '1px solid rgba(255, 255, 255, 0.08)',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(0, 0, 0, 0.25)',
                }}
              >
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '11px', textAlign: 'left' }}>
                  <thead>
                    <tr
                      style={{
                        borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
                        backgroundColor: 'rgba(0, 0, 0, 0.3)',
                        color: '#94a3b8',
                      }}
                    >
                      <th style={{ padding: '5px 8px', width: '30px' }}>#</th>
                      <th style={{ padding: '5px 8px' }}>File : Line</th>
                      <th style={{ padding: '5px 8px' }}>Symbol</th>
                      <th style={{ padding: '5px 8px' }}>Target</th>
                      <th style={{ padding: '5px 8px' }}>Message</th>
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
                        <td style={{ padding: '5px 8px', color: '#64748b' }}>{idx + 1}</td>
                        <td style={{ padding: '5px 8px', fontFamily: 'monospace' }}>
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
                        <td style={{ padding: '5px 8px', color: '#e2e8f0', fontFamily: 'monospace' }}>
                          {v.symbol || '-'}
                        </td>
                        <td style={{ padding: '5px 8px', color: '#cbd5e1', fontFamily: 'monospace' }}>
                          {v.target || '-'}
                        </td>
                        <td style={{ padding: '5px 8px', color: '#f87171' }}>{v.message || '-'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {testResult && testResult.violations.length === 0 && (
              <div
                style={{
                  padding: '8px 12px',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(16, 185, 129, 0.12)',
                  border: '1px solid rgba(16, 185, 129, 0.25)',
                  color: '#6ee7b7',
                  fontSize: '11.5px',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                }}
              >
                <CheckIcon size={14} />
                <span>Zero violations found — invariant strictly holds across workspace graph!</span>
              </div>
            )}
          </div>
        </div>

        {/* Modal Footer Actions */}
        <div
          style={{
            padding: '12px 20px',
            borderTop: '1px solid var(--vscode-panel-border, rgba(255, 255, 255, 0.1))',
            backgroundColor: 'rgba(0, 0, 0, 0.2)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: '12px',
          }}
        >
          {saveError ? (
            <div style={{ color: '#f87171', fontSize: '11.5px', fontWeight: 500 }}>
              {saveError}
            </div>
          ) : (
            <div style={{ color: '#94a3b8', fontSize: '11px' }}>
              Files saved directly to <code style={{ color: '#cbd5e1' }}>.codeexplorer/gates/</code>
            </div>
          )}

          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <button
              type="button"
              onClick={onClose}
              disabled={isSaving}
              style={{
                backgroundColor: 'transparent',
                border: '1px solid var(--vscode-panel-border, #454545)',
                color: 'var(--vscode-editor-foreground, #cbd5e1)',
                borderRadius: '4px',
                padding: '6px 14px',
                fontSize: '12px',
                cursor: 'pointer',
              }}
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleSave}
              disabled={isSaving || !id.trim() || !query.trim()}
              style={{
                backgroundColor: 'var(--vscode-button-background, #0e639c)',
                color: 'var(--vscode-button-foreground, #ffffff)',
                border: 'none',
                borderRadius: '4px',
                padding: '6px 18px',
                fontSize: '12px',
                fontWeight: 600,
                cursor: isSaving || !id.trim() || !query.trim() ? 'not-allowed' : 'pointer',
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
      </div>
    </div>
  );
};
