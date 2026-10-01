import React, { useState, useEffect } from 'react';

export interface DomainOverrideModalProps {
  isOpen: boolean;
  serviceName: string;
  currentDomain?: string;
  currentContext?: string;
  availableDomains?: string[];
  onSave: (serviceName: string, domain: string, context?: string) => void;
  onReset: (serviceName: string) => void;
  onClose: () => void;
}

export const DomainOverrideModal: React.FC<DomainOverrideModalProps> = ({
  isOpen,
  serviceName,
  currentDomain = '',
  currentContext = '',
  availableDomains = [],
  onSave,
  onReset,
  onClose,
}) => {
  const [selectedDomain, setSelectedDomain] = useState<string>(currentDomain);
  const [customDomain, setCustomDomain] = useState<string>('');
  const [isCustom, setIsCustom] = useState<boolean>(false);
  const [context, setContext] = useState<string>(currentContext);

  useEffect(() => {
    if (isOpen) {
      setSelectedDomain(currentDomain || (availableDomains.length > 0 ? availableDomains[0] : ''));
      setCustomDomain('');
      setIsCustom(false);
      setContext(currentContext);
    }
  }, [isOpen, currentDomain, currentContext, availableDomains]);

  if (!isOpen) return null;

  const handleSave = () => {
    const targetDomain = isCustom ? customDomain.trim() : selectedDomain.trim();
    if (!targetDomain) return;
    onSave(serviceName, targetDomain, context.trim() || undefined);
    onClose();
  };

  const handleReset = () => {
    onReset(serviceName);
    onClose();
  };

  return (
    <div
      style={{
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        bottom: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.65)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 9999,
        backdropFilter: 'blur(2px)',
      }}
      onClick={onClose}
    >
      <div
        style={{
          backgroundColor: 'var(--vscode-editor-background, #1e1e1e)',
          border: '1px solid var(--vscode-panel-border, #454545)',
          borderRadius: '8px',
          width: '460px',
          maxWidth: '90vw',
          padding: '24px',
          color: 'var(--vscode-editor-foreground, #cccccc)',
          boxShadow: '0 8px 32px rgba(0, 0, 0, 0.5)',
          display: 'flex',
          flexDirection: 'column',
          gap: '18px',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h3 style={{ margin: 0, fontSize: '1.15rem', color: 'var(--vscode-editor-foreground, #fff)', fontWeight: 600 }}>
            🏷️ Override Service Domain
          </h3>
          <button
            onClick={onClose}
            style={{
              background: 'transparent',
              border: 'none',
              color: 'var(--vscode-editor-foreground, #888)',
              fontSize: '1.2rem',
              cursor: 'pointer',
              lineHeight: 1,
            }}
          >
            ✕
          </button>
        </div>

        <div style={{ fontSize: '0.88rem', color: '#9ca3af' }}>
          Assign service <strong style={{ color: '#38bdf8' }}>{serviceName}</strong> to a specific business domain or bounded context.
        </div>

        {/* Domain Selection */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--vscode-editor-foreground, #ccc)' }}>
            Domain:
          </label>
          <div style={{ display: 'flex', gap: '8px' }}>
            {!isCustom ? (
              <select
                value={selectedDomain}
                onChange={(e) => setSelectedDomain(e.target.value)}
                style={{
                  flex: 1,
                  padding: '8px 12px',
                  backgroundColor: 'var(--vscode-dropdown-background, #252526)',
                  color: 'var(--vscode-dropdown-foreground, #fff)',
                  border: '1px solid var(--vscode-dropdown-border, #3c3c3c)',
                  borderRadius: '4px',
                  fontSize: '0.88rem',
                }}
              >
                {availableDomains.map((d) => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </select>
            ) : (
              <input
                type="text"
                placeholder="Enter new Domain name (e.g. BillingManagement)"
                value={customDomain}
                onChange={(e) => setCustomDomain(e.target.value)}
                style={{
                  flex: 1,
                  padding: '8px 12px',
                  backgroundColor: 'var(--vscode-input-background, #252526)',
                  color: 'var(--vscode-input-foreground, #fff)',
                  border: '1px solid var(--vscode-input-border, #3c3c3c)',
                  borderRadius: '4px',
                  fontSize: '0.88rem',
                }}
              />
            )}
            <button
              onClick={() => setIsCustom(!isCustom)}
              title={isCustom ? 'Pick from existing' : 'Create new domain'}
              style={{
                padding: '8px 12px',
                backgroundColor: 'var(--vscode-button-secondaryBackground, #3a3d41)',
                color: 'var(--vscode-button-secondaryForeground, #fff)',
                border: 'none',
                borderRadius: '4px',
                cursor: 'pointer',
                fontSize: '0.82rem',
                whiteSpace: 'nowrap',
              }}
            >
              {isCustom ? '📋 Existing' : '➕ New'}
            </button>
          </div>
        </div>

        {/* Bounded Context Selection */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
          <label style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--vscode-editor-foreground, #ccc)' }}>
            Bounded Context (optional):
          </label>
          <input
            type="text"
            placeholder="e.g. InvoicingContext (optional)"
            value={context}
            onChange={(e) => setContext(e.target.value)}
            style={{
              padding: '8px 12px',
              backgroundColor: 'var(--vscode-input-background, #252526)',
              color: 'var(--vscode-input-foreground, #fff)',
              border: '1px solid var(--vscode-input-border, #3c3c3c)',
              borderRadius: '4px',
              fontSize: '0.88rem',
            }}
          />
        </div>

        {/* Action Buttons */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '12px', paddingTop: '12px', borderTop: '1px solid var(--vscode-panel-border, #333)' }}>
          <button
            onClick={handleReset}
            title="Reset to default algorithmic or configured domain"
            style={{
              padding: '8px 14px',
              backgroundColor: 'transparent',
              color: '#f87171',
              border: '1px solid rgba(248, 113, 113, 0.4)',
              borderRadius: '4px',
              cursor: 'pointer',
              fontSize: '0.84rem',
            }}
          >
            ↩️ Reset to Default
          </button>
          <div style={{ display: 'flex', gap: '10px' }}>
            <button
              onClick={onClose}
              style={{
                padding: '8px 16px',
                backgroundColor: 'var(--vscode-button-secondaryBackground, #3a3d41)',
                color: 'var(--vscode-button-secondaryForeground, #fff)',
                border: 'none',
                borderRadius: '4px',
                cursor: 'pointer',
                fontSize: '0.86rem',
              }}
            >
              Cancel
            </button>
            <button
              onClick={handleSave}
              disabled={isCustom ? !customDomain.trim() : !selectedDomain.trim()}
              style={{
                padding: '8px 18px',
                backgroundColor: 'var(--vscode-button-background, #0e639c)',
                color: 'var(--vscode-button-foreground, #fff)',
                border: 'none',
                borderRadius: '4px',
                cursor: 'pointer',
                fontSize: '0.86rem',
                fontWeight: 600,
              }}
            >
              💾 Save Override
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
