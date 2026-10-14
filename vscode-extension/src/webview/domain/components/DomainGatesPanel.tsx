import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  GateSuiteSummaryDto,
  GateItemDto,
  GateViolationDto,
  RatchetGatesResponseDto,
} from '../../../../../proto/types';
import { getVsCodeApi } from '../../vscodeApi';
import { FloatingPanel } from '../../components/FloatingPanel';
import {
  ShieldIcon,
  RotateCcwIcon,
  ChevronDownIcon,
  ChevronRightIcon,
  XIcon,
} from '../../components/Icons';

export interface DomainGatesPanelProps {
  serverHttpUrl?: string;
  onOpenFile?: (filePath: string, lineStart?: number) => void;
  onSuiteLoaded?: (suite: GateSuiteSummaryDto) => void;
  docked?: boolean;
  onClose?: () => void;
}

export const DomainGatesPanel: React.FC<DomainGatesPanelProps> = ({
  serverHttpUrl,
  onOpenFile,
  onSuiteLoaded,
  docked = true,
  onClose,
}) => {
  const [suite, setSuite] = useState<GateSuiteSummaryDto | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [actionStatus, setActionStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [expandedGateIds, setExpandedGateIds] = useState<Set<string>>(new Set());

  const resolvedHttpUrl = useMemo(() => {
    if (serverHttpUrl) return serverHttpUrl;
    const wsUrl = typeof window !== 'undefined' ? window.__CE_CONFIG__?.wsUrl : undefined;
    if (wsUrl) {
      return wsUrl.replace(/^ws:\/\//, 'http://').replace(/^wss:\/\//, 'https://').replace(/\/ws$/, '');
    }
    return '';
  }, [serverHttpUrl]);

  const applySuite = useCallback(
    (nextSuite: GateSuiteSummaryDto) => {
      setSuite(nextSuite);
      onSuiteLoaded?.(nextSuite);
      setExpandedGateIds((prev) => {
        if (prev.size > 0) return prev;
        const autoExpand = new Set<string>();
        for (const g of nextSuite.gates || []) {
          if (g.newViolationsCount > 0 || g.status === 'FailedNew' || g.status === 'Error') {
            autoExpand.add(g.id);
          }
        }
        return autoExpand;
      });
    },
    [onSuiteLoaded]
  );

  const fetchGates = useCallback(async () => {
    if (!resolvedHttpUrl) return;
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${resolvedHttpUrl}/api/gates`);
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      }
      const data = (await res.json()) as GateSuiteSummaryDto;
      applySuite(data);
    } catch (err: any) {
      setError(err?.message || 'Failed to load gates');
    } finally {
      setLoading(false);
    }
  }, [resolvedHttpUrl, applySuite]);

  useEffect(() => {
    const vscode = getVsCodeApi();
    if (vscode) {
      vscode.postMessage({ type: 'GET_GATES' });
    } else {
      fetchGates();
    }
  }, [fetchGates]);

  useEffect(() => {
    const handleMessage = (event: MessageEvent) => {
      const msg = event.data;
      if (msg?.type === 'GATES_UPDATED') {
        if (msg.suite) {
          applySuite(msg.suite as GateSuiteSummaryDto);
        }
        if (typeof msg.isEvaluating === 'boolean') {
          setLoading(msg.isEvaluating);
        }
        if (msg.actionStatus !== undefined) {
          setActionStatus(msg.actionStatus);
        }
        if (msg.error) {
          setError(msg.error);
        }
      }
    };
    window.addEventListener('message', handleMessage);
    return () => window.removeEventListener('message', handleMessage);
  }, [applySuite]);

  const handleEvaluate = useCallback(
    async (gateId?: string) => {
      const vscode = getVsCodeApi();
      if (vscode) {
        setLoading(true);
        setError(null);
        setActionStatus(gateId ? `Evaluating ${gateId}...` : 'Evaluating all gates...');
        vscode.postMessage({ type: 'EVALUATE_GATES', gateId });
        return;
      }

      if (!resolvedHttpUrl) return;
      setLoading(true);
      setError(null);
      setActionStatus(gateId ? `Evaluating ${gateId}...` : 'Evaluating all gates...');
      try {
        const res = await fetch(`${resolvedHttpUrl}/api/gates/check`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(gateId ? { gateId } : {}),
        });
        if (!res.ok) {
          throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        }
        const data = (await res.json()) as GateSuiteSummaryDto;
        applySuite(data);
        setActionStatus(
          data.isSuccess
            ? `Passed (${data.passedCount}/${data.totalGates})`
            : `${data.failedCount} gate(s) failed (${data.newViolationsCount} new)`
        );
      } catch (err: any) {
        setError(err?.message || 'Evaluation failed');
        setActionStatus(null);
      } finally {
        setLoading(false);
      }
    },
    [resolvedHttpUrl, applySuite]
  );

  const handleRatchet = useCallback(
    async (gateId?: string) => {
      const vscode = getVsCodeApi();
      if (vscode) {
        setLoading(true);
        setError(null);
        setActionStatus(gateId ? `Ratcheting ${gateId}...` : 'Ratcheting baselines...');
        vscode.postMessage({ type: 'RATCHET_GATES', gateId });
        return;
      }

      if (!resolvedHttpUrl) return;
      setLoading(true);
      setError(null);
      setActionStatus(gateId ? `Ratcheting ${gateId}...` : 'Ratcheting baselines...');
      try {
        const res = await fetch(`${resolvedHttpUrl}/api/gates/ratchet`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(gateId ? { gateId } : { updateAll: true }),
        });
        if (!res.ok) {
          throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        }
        const data = (await res.json()) as RatchetGatesResponseDto;
        if (data.suite) {
          applySuite(data.suite);
        } else {
          await fetchGates();
        }
        setActionStatus(data.message || `Updated ${data.updatedGatesCount} baseline(s)`);
      } catch (err: any) {
        setError(err?.message || 'Ratchet failed');
        setActionStatus(null);
      } finally {
        setLoading(false);
      }
    },
    [resolvedHttpUrl, applySuite, fetchGates]
  );

  const handleInitStarterGate = useCallback(async () => {
    const vscode = getVsCodeApi();
    if (vscode) {
      setLoading(true);
      setError(null);
      setActionStatus('Initializing starter gate...');
      vscode.postMessage({ type: 'INIT_GATES' });
      return;
    }

    if (!resolvedHttpUrl) return;
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`${resolvedHttpUrl}/api/gates/init`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ overwrite: false }),
      });
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}: ${res.statusText}`);
      }
      const data = (await res.json()) as RatchetGatesResponseDto;
      if (data.suite) {
        applySuite(data.suite);
      }
      setActionStatus(data.message || 'Starter gate created in .codeexplorer/gates/');
      onOpenFile?.('.codeexplorer/gates/example-gate.yaml.example', 1);
    } catch (err: any) {
      setError(err?.message || 'Failed to initialize starter gate');
    } finally {
      setLoading(false);
    }
  }, [resolvedHttpUrl, applySuite, onOpenFile]);

  const toggleGateExpand = useCallback((gateId: string) => {
    setExpandedGateIds((prev) => {
      const next = new Set(prev);
      if (next.has(gateId)) {
        next.delete(gateId);
      } else {
        next.add(gateId);
      }
      return next;
    });
  }, []);

  const filteredGates = useMemo(() => {
    const gates = suite?.gates || [];
    const q = searchQuery.trim().toLowerCase();
    if (!q) return gates;
    return gates.filter(
      (g) =>
        g.id.toLowerCase().includes(q) ||
        g.name.toLowerCase().includes(q) ||
        (g.description || '').toLowerCase().includes(q) ||
        g.category.toLowerCase().includes(q) ||
        g.violations.some(
          (v) =>
            (v.symbol || '').toLowerCase().includes(q) ||
            (v.target || '').toLowerCase().includes(q) ||
            (v.file || '').toLowerCase().includes(q)
        )
    );
  }, [suite, searchQuery]);

  const totalGates = suite?.totalGates || 0;
  const failedCount = suite?.failedCount || 0;
  const newViolationsCount = suite?.newViolationsCount || 0;
  const toleratedCount = suite?.toleratedViolationsCount || 0;

  const isEvaluated = useMemo(() => {
    if (!suite || !suite.gates || suite.gates.length === 0) return false;
    return suite.gates.some((g) => g.status !== 'Pending');
  }, [suite]);

  return (
    <FloatingPanel
      dock="top-right"
      isCollapsed={false}
      icon={<ShieldIcon size={14} />}
      title="Invariant Gates"
      ariaLabel="Semantic Invariant Gates"
      badge={
        totalGates > 0 ? (
          <span
            className="floating-dock-badge"
            style={{
              backgroundColor: !isEvaluated
                ? 'rgba(148, 163, 184, 0.15)'
                : failedCount > 0
                ? 'rgba(239, 68, 68, 0.2)'
                : toleratedCount > 0
                ? 'rgba(245, 158, 11, 0.2)'
                : 'rgba(16, 185, 129, 0.2)',
              color: !isEvaluated
                ? '#94a3b8'
                : failedCount > 0
                ? '#fca5a5'
                : toleratedCount > 0
                ? '#fcd34d'
                : '#6ee7b7',
              border: `1px solid ${
                !isEvaluated
                  ? 'rgba(148, 163, 184, 0.3)'
                  : failedCount > 0
                  ? 'rgba(239, 68, 68, 0.35)'
                  : toleratedCount > 0
                  ? 'rgba(245, 158, 11, 0.35)'
                  : 'rgba(16, 185, 129, 0.35)'
              }`,
              fontSize: '10px',
              padding: '1px 6px',
            }}
          >
            {!isEvaluated ? `${totalGates} gates` : `${suite?.passedCount ?? 0}/${totalGates}`}
          </span>
        ) : undefined
      }
      headerActions={
        <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
          <button
            type="button"
            className="floating-dock-btn action-btn"
            onClick={() => handleEvaluate()}
            disabled={loading}
            title="Evaluate all Semantic Invariant Gates"
          >
            <RotateCcwIcon size={11} />
            <span>Check</span>
          </button>
          {totalGates > 0 && (
            <button
              type="button"
              className="floating-dock-btn action-btn"
              onClick={() => handleRatchet()}
              disabled={loading}
              title="Ratchet all gate baselines to current violation fingerprints"
            >
              <span>Ratchet All</span>
            </button>
          )}
        </div>
      }
      subtitle={
        totalGates > 0 ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', width: '100%' }}>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                fontSize: '11px',
                color: '#94a3b8',
              }}
            >
              {!isEvaluated ? (
                <span>
                  <strong style={{ color: '#e2e8f0' }}>{totalGates}</strong> gates configured · Click &quot;Check&quot; to evaluate
                </span>
              ) : (
                <span>
                  <strong style={{ color: '#e2e8f0' }}>{suite?.passedCount ?? 0}</strong> passed ·{' '}
                  <strong style={{ color: newViolationsCount > 0 ? '#f87171' : '#94a3b8' }}>
                    {newViolationsCount}
                  </strong>{' '}
                  new ·{' '}
                  <strong style={{ color: toleratedCount > 0 ? '#fbbf24' : '#94a3b8' }}>
                    {toleratedCount}
                  </strong>{' '}
                  tolerated
                </span>
              )}
              {isEvaluated && suite?.totalDurationMs !== undefined && (
                <span>{Math.round(suite.totalDurationMs)}ms</span>
              )}
            </div>
            {totalGates > 2 && (
              <div className="domain-hidden-search-wrap" style={{ width: '100%', margin: 0 }}>
                <input
                  type="text"
                  className="domain-hidden-search-input"
                  placeholder="Filter gates or violations..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                />
                {searchQuery && (
                  <button
                    type="button"
                    className="domain-hidden-search-clear"
                    onClick={() => setSearchQuery('')}
                    title="Clear filter"
                  >
                    <XIcon size={11} />
                  </button>
                )}
              </div>
            )}
          </div>
        ) : undefined
      }
      className="domain-gates-panel no-body-padding"
      width={360}
      minWidth={280}
      maxWidth={700}
      persistKey="ce_gates_panel"
      docked={docked}
      onClose={onClose}
    >
      <div
        className="domain-gates-body"
        style={{
          flex: 1,
          minHeight: 0,
          overflowY: 'auto',
          overflowX: 'hidden',
          padding: '8px 10px',
          display: 'flex',
          flexDirection: 'column',
          gap: '8px',
        }}
      >
        {actionStatus && (
          <div
            style={{
              fontSize: '11px',
              padding: '5px 8px',
              borderRadius: '6px',
              background: 'rgba(56, 189, 248, 0.1)',
              border: '1px solid rgba(56, 189, 248, 0.25)',
              color: '#bae6fd',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
            }}
          >
            <span>{actionStatus}</span>
            <button
              type="button"
              onClick={() => setActionStatus(null)}
              style={{
                background: 'none',
                border: 'none',
                color: '#94a3b8',
                cursor: 'pointer',
                padding: 0,
              }}
            >
              <XIcon size={11} />
            </button>
          </div>
        )}

        {error && (
          <div
            style={{
              fontSize: '11px',
              padding: '6px 8px',
              borderRadius: '6px',
              background: 'rgba(239, 68, 68, 0.12)',
              border: '1px solid rgba(239, 68, 68, 0.35)',
              color: '#fca5a5',
            }}
          >
            {error}
          </div>
        )}

        {loading && !suite && (
          <div style={{ padding: '16px', textAlign: 'center', color: '#94a3b8', fontSize: '12px' }}>
            Evaluating Semantic Invariant Gates...
          </div>
        )}

        {!loading && totalGates === 0 && (
          <div className="domain-panel-empty-state">
            <div className="domain-empty-card">
              <span className="domain-empty-icon">🛡️</span>
              <h4 className="domain-empty-title">No Gates Configured</h4>
              <p className="domain-empty-desc">
                Define architectural invariants as OpenCypher rules in{' '}
                <code>.codeexplorer/gates/*.yaml</code> with automatic ratchet baselines.
              </p>
              <button
                type="button"
                className="floating-dock-btn action-btn"
                style={{ marginTop: '10px', padding: '5px 12px' }}
                onClick={handleInitStarterGate}
              >
                + Initialize Starter Gate
              </button>
            </div>
          </div>
        )}

        {filteredGates.map((gate: GateItemDto) => {
          const isExpanded = expandedGateIds.has(gate.id);
          const isPending = gate.status === 'Pending';
          const isGateEvaluating = gate.status === 'Evaluating' || (loading && actionStatus?.includes(gate.id));
          const isFailed = gate.status === 'FailedNew' || gate.status === 'Error';
          const isTolerated = gate.status === 'PassedTolerated';
          const statusColor = isGateEvaluating
            ? '#38bdf8'
            : isPending
            ? '#94a3b8'
            : isFailed
            ? '#f87171'
            : isTolerated
            ? '#fbbf24'
            : '#34d399';
          const statusBg = isGateEvaluating
            ? 'rgba(56, 189, 248, 0.14)'
            : isPending
            ? 'rgba(148, 163, 184, 0.14)'
            : isFailed
            ? 'rgba(239, 68, 68, 0.14)'
            : isTolerated
            ? 'rgba(245, 158, 11, 0.14)'
            : 'rgba(16, 185, 129, 0.14)';
          const statusLabel = isGateEvaluating
            ? 'EVALUATING...'
            : isPending
            ? 'NOT CHECKED'
            : isFailed
            ? `${gate.newViolationsCount} NEW`
            : isTolerated
            ? `${gate.toleratedViolationsCount} TOLERATED`
            : 'PASSED';

          return (
            <div
              key={gate.id}
              style={{
                borderRadius: '8px',
                border: `1px solid ${
                  isFailed ? 'rgba(239, 68, 68, 0.35)' : 'rgba(148, 163, 184, 0.18)'
                }`,
                background: 'rgba(15, 23, 42, 0.65)',
                overflow: 'hidden',
                flexShrink: 0,
              }}
            >
              <div
                onClick={() => toggleGateExpand(gate.id)}
                style={{
                  padding: '8px 10px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: '8px',
                  cursor: 'pointer',
                  userSelect: 'none',
                  minHeight: '38px',
                  boxSizing: 'border-box',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px', minWidth: 0, flex: 1 }}>
                  <span
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      width: 14,
                      height: 14,
                      flexShrink: 0,
                      color: '#94a3b8',
                    }}
                  >
                    {isExpanded ? <ChevronDownIcon size={12} /> : <ChevronRightIcon size={12} />}
                  </span>
                  <div style={{ minWidth: 0, flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
                    <div
                      style={{
                        fontSize: '12px',
                        fontWeight: 600,
                        color: '#f1f5f9',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        whiteSpace: 'nowrap',
                        lineHeight: 1.3,
                      }}
                      title={`${gate.name} (${gate.id})`}
                    >
                      {gate.name || gate.id}
                    </div>
                    <div style={{ fontSize: '10px', color: '#94a3b8', lineHeight: 1.2 }}>
                      {gate.id} · {gate.category} · {gate.severity}
                    </div>
                  </div>
                </div>
                <span
                  style={{
                    fontSize: '9.5px',
                    fontWeight: 700,
                    padding: '2px 6px',
                    borderRadius: '4px',
                    color: statusColor,
                    background: statusBg,
                    flexShrink: 0,
                    lineHeight: 1.2,
                    whiteSpace: 'nowrap',
                  }}
                >
                  {statusLabel}
                </span>
              </div>

              {isExpanded && (
                <div
                  style={{
                    padding: '8px 10px',
                    borderTop: '1px solid rgba(148, 163, 184, 0.12)',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '6px',
                    fontSize: '11px',
                  }}
                >
                  {gate.description && (
                    <div style={{ color: '#cbd5e1', lineHeight: 1.4 }}>{gate.description}</div>
                  )}
                  {gate.remediation && (
                    <div
                      style={{
                        color: '#93c5fd',
                        background: 'rgba(59, 130, 246, 0.1)',
                        padding: '5px 7px',
                        borderRadius: '4px',
                        fontSize: '10.5px',
                      }}
                    >
                      💡 {gate.remediation}
                    </div>
                  )}
                  {gate.errorMessage && (
                    <div style={{ color: '#fca5a5', fontSize: '10.5px' }}>⚠️ {gate.errorMessage}</div>
                  )}

                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px', marginTop: '2px' }}>
                    <button
                      type="button"
                      className="floating-dock-btn action-btn"
                      onClick={(e) => {
                        e.stopPropagation();
                        handleEvaluate(gate.id);
                      }}
                      disabled={loading}
                    >
                      {isPending ? 'Evaluate' : 'Re-evaluate'}
                    </button>
                    {gate.ratchetEnabled && (gate.newViolationsCount > 0 || gate.canRatchetDown) && (
                      <button
                        type="button"
                        className="floating-dock-btn action-btn"
                        onClick={(e) => {
                          e.stopPropagation();
                          handleRatchet(gate.id);
                        }}
                        disabled={loading}
                        title="Update baseline fingerprint file for this gate"
                      >
                        {gate.canRatchetDown ? 'Ratchet Down ↓' : 'Baseline Current'}
                      </button>
                    )}
                  </div>

                  {gate.violations && gate.violations.length > 0 && (
                    <div
                      style={{
                        display: 'flex',
                        flexDirection: 'column',
                        gap: '4px',
                        marginTop: '4px',
                        maxHeight: '240px',
                        overflowY: 'auto',
                      }}
                    >
                      {gate.violations.slice(0, 100).map((v: GateViolationDto, idx: number) => (
                        <div
                          key={`${v.fingerprint}-${idx}`}
                          onClick={() => {
                            if (v.file && onOpenFile) {
                              onOpenFile(v.file, v.line || 1);
                            }
                          }}
                          style={{
                            padding: '5px 7px',
                            borderRadius: '5px',
                            background: v.isTolerated
                              ? 'rgba(30, 41, 59, 0.65)'
                              : 'rgba(239, 68, 68, 0.1)',
                            border: `1px solid ${
                              v.isTolerated
                                ? 'rgba(148, 163, 184, 0.15)'
                                : 'rgba(239, 68, 68, 0.3)'
                            }`,
                            cursor: v.file ? 'pointer' : 'default',
                            display: 'flex',
                            flexDirection: 'column',
                            gap: '2px',
                          }}
                          title={v.file ? `Click to open ${v.file}:${v.line || 1}` : v.fingerprint}
                        >
                          <div
                            style={{
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'space-between',
                              gap: '6px',
                            }}
                          >
                            <span
                              style={{
                                fontWeight: 600,
                                color: '#e2e8f0',
                                overflow: 'hidden',
                                textOverflow: 'ellipsis',
                                whiteSpace: 'nowrap',
                              }}
                            >
                              {v.symbol || v.target || v.file || 'Violation'}
                            </span>
                            <span
                              style={{
                                fontSize: '9px',
                                padding: '1px 4px',
                                borderRadius: '3px',
                                color: v.isTolerated ? '#94a3b8' : '#fca5a5',
                                background: v.isTolerated
                                  ? 'rgba(148, 163, 184, 0.15)'
                                  : 'rgba(239, 68, 68, 0.2)',
                                flexShrink: 0,
                              }}
                            >
                              {v.isTolerated ? 'TOLERATED' : 'NEW'}
                            </span>
                          </div>
                          {v.target && v.symbol && (
                            <div style={{ fontSize: '10px', color: '#94a3b8' }}>→ {v.target}</div>
                          )}
                          {v.file && (
                            <div style={{ fontSize: '10px', color: '#38bdf8' }}>
                              📄 {v.file}
                              {v.line > 0 ? `:${v.line}` : ''}
                            </div>
                          )}
                        </div>
                      ))}
                      {gate.violations.length > 100 && (
                        <div
                          style={{
                            fontSize: '10.5px',
                            color: '#94a3b8',
                            textAlign: 'center',
                            padding: '6px 4px',
                            background: 'rgba(15, 23, 42, 0.4)',
                            borderRadius: '4px',
                            border: '1px dashed rgba(148, 163, 184, 0.2)',
                          }}
                        >
                          Showing first 100 of {gate.violations.length.toLocaleString()} violations
                        </div>
                      )}
                    </div>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </FloatingPanel>
  );
};
