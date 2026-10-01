import React, { useState, useEffect } from 'react';

export type AiProvider = 'builtin' | 'openai' | 'gguf';

export interface ModelDescriptor {
  id: string;
  name: string;
  vendor: string;
  family: string;
  provider: AiProvider;
  description?: string;
}

export interface AiSettings {
  provider: AiProvider;
  model: string;
  endpoint: string;
  apiKey: string;
}

export interface ModelStatus {
  exists: boolean;
  modelPath?: string;
  sizeBytes?: number;
  sizeMb?: string;
}

export interface SettingsViewProps {
  initialSettings?: Partial<AiSettings>;
  availableBuiltinModels?: ModelDescriptor[];
  availableExternalModels?: ModelDescriptor[];
  ggufStatus?: ModelStatus;
  onSaveSettings: (settings: AiSettings) => void;
  onTestConnection: (settings: AiSettings) => void;
  onDownloadGguf?: () => void;
  onClose: () => void;
  testResult?: { success: boolean; message: string } | null;
  isTesting?: boolean;
}

export const SettingsView: React.FC<SettingsViewProps> = ({
  initialSettings,
  availableBuiltinModels = [],
  availableExternalModels = [],
  ggufStatus,
  onSaveSettings,
  onTestConnection,
  onDownloadGguf,
  onClose,
  testResult,
  isTesting = false,
}) => {
  const [provider, setProvider] = useState<AiProvider>(initialSettings?.provider || 'builtin');
  const [model, setModel] = useState<string>(initialSettings?.model || '');
  const [endpoint, setEndpoint] = useState<string>(initialSettings?.endpoint || 'http://localhost:11434/v1');
  const [apiKey, setApiKey] = useState<string>(initialSettings?.apiKey || '');
  const [saveSuccess, setSaveSuccess] = useState<boolean>(false);

  useEffect(() => {
    if (initialSettings?.provider) setProvider(initialSettings.provider);
    if (initialSettings?.model !== undefined) setModel(initialSettings.model);
    if (initialSettings?.endpoint !== undefined) setEndpoint(initialSettings.endpoint);
    if (initialSettings?.apiKey !== undefined) setApiKey(initialSettings.apiKey);
  }, [initialSettings]);

  const handleSave = () => {
    onSaveSettings({
      provider,
      model,
      endpoint,
      apiKey,
    });
    setSaveSuccess(true);
    setTimeout(() => setSaveSuccess(false), 3000);
  };

  const handleTest = () => {
    onTestConnection({
      provider,
      model,
      endpoint,
      apiKey,
    });
  };

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
      }}
    >
      {/* Header */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          borderBottom: '1px solid var(--vscode-panel-border, #333)',
          paddingBottom: '16px',
          marginBottom: '24px',
        }}
      >
        <div>
          <h2 style={{ fontSize: '1.4rem', fontWeight: 600, color: 'var(--vscode-foreground, #fff)', margin: 0 }}>
            ⚙️ AI & Model Settings
          </h2>
          <p style={{ margin: '4px 0 0', fontSize: '0.88rem', opacity: 0.8 }}>
            Configure LLM providers for architectural domain discovery, intent distillation, and bounded context clustering.
          </p>
        </div>
        <button
          onClick={onClose}
          style={{
            padding: '6px 14px',
            backgroundColor: 'var(--vscode-button-secondaryBackground, #3a3d41)',
            color: 'var(--vscode-button-secondaryForeground, #ffffff)',
            border: 'none',
            borderRadius: '4px',
            cursor: 'pointer',
            fontWeight: 500,
          }}
        >
          ✕ Close
        </button>
      </div>

      {/* Provider Selector Cards */}
      <div style={{ marginBottom: '28px' }}>
        <label style={{ display: 'block', fontSize: '0.9rem', fontWeight: 600, marginBottom: '10px' }}>
          Inference Engine Provider
        </label>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '14px' }}>
          {/* Built-in IDE Model */}
          <div
            onClick={() => setProvider('builtin')}
            style={{
              padding: '16px',
              borderRadius: '6px',
              border: `2px solid ${provider === 'builtin' ? 'var(--vscode-focusBorder, #007fd4)' : 'var(--vscode-panel-border, #333)'}`,
              backgroundColor: provider === 'builtin' ? 'rgba(0, 127, 212, 0.1)' : 'var(--vscode-sideBar-background, #252526)',
              cursor: 'pointer',
              transition: 'all 0.2s',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
              <span style={{ fontSize: '1.2rem' }}>✨</span>
              <strong style={{ fontSize: '0.95rem', color: '#38bdf8' }}>Built-in IDE Model</strong>
              <span
                style={{
                  fontSize: '0.7rem',
                  backgroundColor: '#059669',
                  color: '#fff',
                  padding: '2px 6px',
                  borderRadius: '3px',
                  fontWeight: 600,
                  marginLeft: 'auto',
                }}
              >
                RECOMMENDED
              </span>
            </div>
            <p style={{ margin: 0, fontSize: '0.82rem', opacity: 0.85, lineHeight: 1.4 }}>
              Uses VS Code Copilot or Antigravity Gemini via <code>vscode.lm</code>. 0 MB download, 0 GPU load, zero configuration.
            </p>
          </div>

          {/* External OpenAI-Compatible */}
          <div
            onClick={() => setProvider('openai')}
            style={{
              padding: '16px',
              borderRadius: '6px',
              border: `2px solid ${provider === 'openai' ? 'var(--vscode-focusBorder, #007fd4)' : 'var(--vscode-panel-border, #333)'}`,
              backgroundColor: provider === 'openai' ? 'rgba(0, 127, 212, 0.1)' : 'var(--vscode-sideBar-background, #252526)',
              cursor: 'pointer',
              transition: 'all 0.2s',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
              <span style={{ fontSize: '1.2rem' }}>🌐</span>
              <strong style={{ fontSize: '0.95rem', color: '#fbbf24' }}>OpenAI / Ollama / LM Studio</strong>
            </div>
            <p style={{ margin: 0, fontSize: '0.82rem', opacity: 0.85, lineHeight: 1.4 }}>
              Connect to a local Ollama server, LM Studio, vLLM, DeepSeek, or any OpenAI-compatible API endpoint.
            </p>
          </div>

          {/* Local GGUF Model */}
          <div
            onClick={() => setProvider('gguf')}
            style={{
              padding: '16px',
              borderRadius: '6px',
              border: `2px solid ${provider === 'gguf' ? 'var(--vscode-focusBorder, #007fd4)' : 'var(--vscode-panel-border, #333)'}`,
              backgroundColor: provider === 'gguf' ? 'rgba(0, 127, 212, 0.1)' : 'var(--vscode-sideBar-background, #252526)',
              cursor: 'pointer',
              transition: 'all 0.2s',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
              <span style={{ fontSize: '1.2rem' }}>📦</span>
              <strong style={{ fontSize: '0.95rem', color: '#a78bfa' }}>Embedded GGUF (llama.cpp)</strong>
            </div>
            <p style={{ margin: 0, fontSize: '0.82rem', opacity: 0.85, lineHeight: 1.4 }}>
              Self-contained offline execution using Qwen 2.5 7B Q4_K_M model. Requires ~940 MB disk download.
            </p>
          </div>
        </div>
      </div>

      {/* Provider Detailed Configuration Form */}
      <div
        style={{
          backgroundColor: 'var(--vscode-sideBar-background, #252526)',
          border: '1px solid var(--vscode-panel-border, #333)',
          borderRadius: '6px',
          padding: '20px',
          marginBottom: '24px',
        }}
      >
        {provider === 'builtin' && (
          <div>
            <h3 style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '14px', color: '#38bdf8' }}>
              Built-in IDE Model Configuration
            </h3>
            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '0.85rem', marginBottom: '6px' }}>
                Select Active Language Model:
              </label>
              <select
                value={model}
                onChange={(e) => setModel(e.target.value)}
                style={{
                  width: '100%',
                  maxWidth: '480px',
                  padding: '8px 10px',
                  backgroundColor: 'var(--vscode-input-background, #3c3c3c)',
                  color: 'var(--vscode-input-foreground, #fff)',
                  border: '1px solid var(--vscode-input-border, #555)',
                  borderRadius: '4px',
                  fontSize: '0.9rem',
                }}
              >
                <option value="">-- Automatic (Default Active IDE Model) --</option>
                {availableBuiltinModels.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.name} ({m.vendor} - {m.family})
                  </option>
                ))}
              </select>
              <p style={{ margin: '6px 0 0', fontSize: '0.8rem', opacity: 0.75 }}>
                {availableBuiltinModels.length > 0
                  ? `Discovered ${availableBuiltinModels.length} models registered in your IDE.`
                  : 'Automatic fallback will use the currently active chat assistant (GitHub Copilot or Antigravity Gemini).'}
              </p>
            </div>
          </div>
        )}

        {provider === 'openai' && (
          <div>
            <h3 style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '14px', color: '#fbbf24' }}>
              OpenAI-Compatible Server Configuration
            </h3>
            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '0.85rem', marginBottom: '6px' }}>Endpoint URL:</label>
              <input
                type="text"
                value={endpoint}
                onChange={(e) => setEndpoint(e.target.value)}
                placeholder="http://localhost:11434/v1"
                style={{
                  width: '100%',
                  maxWidth: '480px',
                  padding: '8px 10px',
                  backgroundColor: 'var(--vscode-input-background, #3c3c3c)',
                  color: 'var(--vscode-input-foreground, #fff)',
                  border: '1px solid var(--vscode-input-border, #555)',
                  borderRadius: '4px',
                  fontSize: '0.9rem',
                }}
              />
              <p style={{ margin: '4px 0 0', fontSize: '0.78rem', opacity: 0.75 }}>
                Standard base URL for chat completions (e.g. <code>http://localhost:11434/v1</code> for Ollama, <code>http://localhost:1234/v1</code> for LM Studio).
              </p>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '0.85rem', marginBottom: '6px' }}>Model Name / ID:</label>
              <input
                type="text"
                value={model}
                onChange={(e) => setModel(e.target.value)}
                placeholder="qwen2.5-coder:7b"
                style={{
                  width: '100%',
                  maxWidth: '480px',
                  padding: '8px 10px',
                  backgroundColor: 'var(--vscode-input-background, #3c3c3c)',
                  color: 'var(--vscode-input-foreground, #fff)',
                  border: '1px solid var(--vscode-input-border, #555)',
                  borderRadius: '4px',
                  fontSize: '0.9rem',
                }}
              />
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '0.85rem', marginBottom: '6px' }}>API Key (Optional):</label>
              <input
                type="password"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder="sk-..."
                style={{
                  width: '100%',
                  maxWidth: '480px',
                  padding: '8px 10px',
                  backgroundColor: 'var(--vscode-input-background, #3c3c3c)',
                  color: 'var(--vscode-input-foreground, #fff)',
                  border: '1px solid var(--vscode-input-border, #555)',
                  borderRadius: '4px',
                  fontSize: '0.9rem',
                }}
              />
            </div>
          </div>
        )}

        {provider === 'gguf' && (
          <div>
            <h3 style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '14px', color: '#a78bfa' }}>
              Local GGUF Offline Model
            </h3>
            <div style={{ marginBottom: '14px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                <span style={{ fontSize: '1rem' }}>Status:</span>
                {ggufStatus?.exists ? (
                  <span
                    style={{
                      backgroundColor: 'rgba(16, 185, 129, 0.2)',
                      color: '#34d399',
                      border: '1px solid #059669',
                      padding: '3px 8px',
                      borderRadius: '4px',
                      fontSize: '0.85rem',
                      fontWeight: 600,
                    }}
                  >
                    ✓ Model Installed ({ggufStatus.sizeMb} MB)
                  </span>
                ) : (
                  <span
                    style={{
                      backgroundColor: 'rgba(239, 68, 68, 0.2)',
                      color: '#f87171',
                      border: '1px solid #dc2626',
                      padding: '3px 8px',
                      borderRadius: '4px',
                      fontSize: '0.85rem',
                      fontWeight: 600,
                    }}
                  >
                    ✗ Model Not Found (~940 MB required)
                  </span>
                )}
              </div>
              {ggufStatus?.modelPath && (
                <div style={{ marginTop: '6px', fontSize: '0.8rem', opacity: 0.75, wordBreak: 'break-all' }}>
                  Path: <code>{ggufStatus.modelPath}</code>
                </div>
              )}
            </div>

            {!ggufStatus?.exists && onDownloadGguf && (
              <button
                onClick={onDownloadGguf}
                style={{
                  padding: '8px 16px',
                  backgroundColor: '#7c3aed',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: '4px',
                  cursor: 'pointer',
                  fontWeight: 600,
                  fontSize: '0.88rem',
                }}
              >
                ⬇ Download Qwen-2.5 SLM (~940 MB)
              </button>
            )}
          </div>
        )}

        {/* Test Connection and Feedback */}
        <div style={{ marginTop: '18px', paddingTop: '16px', borderTop: '1px solid var(--vscode-panel-border, #333)' }}>
          <button
            onClick={handleTest}
            disabled={isTesting}
            style={{
              padding: '6px 14px',
              backgroundColor: 'var(--vscode-button-secondaryBackground, #3a3d41)',
              color: 'var(--vscode-button-secondaryForeground, #ffffff)',
              border: 'none',
              borderRadius: '4px',
              cursor: isTesting ? 'wait' : 'pointer',
              fontWeight: 500,
              fontSize: '0.85rem',
              marginRight: '12px',
            }}
          >
            {isTesting ? 'Testing connection...' : '🔌 Test Connection'}
          </button>

          {testResult && (
            <span
              style={{
                fontSize: '0.85rem',
                color: testResult.success ? '#34d399' : '#f87171',
                fontWeight: 500,
              }}
            >
              {testResult.success ? '✓ ' : '✗ '}
              {testResult.message}
            </span>
          )}
        </div>
      </div>

      {/* Save Button */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '14px', marginTop: 'auto' }}>
        <button
          onClick={handleSave}
          style={{
            padding: '10px 24px',
            backgroundColor: 'var(--vscode-button-background, #0e639c)',
            color: 'var(--vscode-button-foreground, #ffffff)',
            border: 'none',
            borderRadius: '4px',
            cursor: 'pointer',
            fontWeight: 600,
            fontSize: '0.92rem',
          }}
        >
          💾 Save Settings
        </button>
        {saveSuccess && (
          <span style={{ color: '#34d399', fontSize: '0.88rem', fontWeight: 600 }}>
            ✓ Settings saved successfully!
          </span>
        )}
      </div>
    </div>
  );
};
