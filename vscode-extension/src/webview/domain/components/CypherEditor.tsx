import React, { useRef, useEffect, useMemo, useCallback } from 'react';

export interface CypherEditorProps {
  value: string;
  onChange: (val: string) => void;
  placeholder?: string;
  readOnly?: boolean;
  minHeight?: number | string;
  maxHeight?: number | string;
  onRunTest?: () => void;
}

/**
 * Escapes HTML characters for safe rendering in the syntax highlight backdrop.
 */
function escapeHtml(str: string): string {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/**
 * High-performance regex-based OpenCypher syntax highlighter.
 */
function highlightCypher(code: string): string {
  if (!code) return '';

  const lines = code.split('\n');
  const highlightedLines = lines.map((line) => {
    // 1. Line comments
    const commentIdx = line.indexOf('//');
    let codePart = commentIdx >= 0 ? line.substring(0, commentIdx) : line;
    const commentPart = commentIdx >= 0 ? line.substring(commentIdx) : '';

    // 2. Tokenize strings
    const stringTokens: string[] = [];
    const placeholderPrefix = `___STR_TOKEN_${Math.random().toString(36).substring(2, 6)}_`;
    codePart = codePart.replace(/(['"])(?:(?=(\\?))\2.)*?\1/g, (match) => {
      const idx = stringTokens.length;
      stringTokens.push(`<span style="color:#ce9178">${escapeHtml(match)}</span>`);
      return `${placeholderPrefix}${idx}___`;
    });

    // 3. Escape raw code
    let escaped = escapeHtml(codePart);

    // 4. Highlight OpenCypher Keywords
    const keywordRegex =
      /\b(MATCH|OPTIONAL\s+MATCH|WHERE|RETURN|WITH|AND|OR|NOT|IN|AS|ORDER\s+BY|LIMIT|SKIP|UNION|DISTINCT|IS\s+NULL|IS\s+NOT\s+NULL|STARTS\s+WITH|ENDS\s+WITH|CONTAINS|CREATE|DELETE|DETACH\s+DELETE|SET|REMOVE|CASE|WHEN|THEN|ELSE|END|DESC|ASC)\b/gi;
    escaped = escaped.replace(
      keywordRegex,
      (m) => `<span style="color:#569cd6;font-weight:600">${m.toUpperCase()}</span>`
    );

    // 5. Highlight Functions
    const funcRegex =
      /\b(coalesce|count|labels|keys|json_extract|toString|toLower|toUpper|trim|split|length|size|exists|head|tail|last|nodes|relationships|id)\b/gi;
    escaped = escaped.replace(
      funcRegex,
      (m) => `<span style="color:#dcdcaa">${m}</span>`
    );

    // 6. Highlight Node Labels (e.g. :Function, :Type, :Project)
    escaped = escaped.replace(
      /(:[A-Za-z0-9_]+)/g,
      `<span style="color:#4ec9b0;font-weight:500">$1</span>`
    );

    // 7. Highlight Relationship Types (e.g. -[:CALLS]->, -[:SERVICE_CALL]->)
    escaped = escaped.replace(
      /(-\[:[A-Za-z0-9_]+\]->|-\[:[A-Za-z0-9_]+\]-|\[:[A-Za-z0-9_]+\])/g,
      `<span style="color:#4fc1ff;font-weight:500">$1</span>`
    );

    // 8. Highlight Numbers
    escaped = escaped.replace(
      /\b(\d+(?:\.\d+)?)\b/g,
      `<span style="color:#b5cea8">$1</span>`
    );

    // 9. Highlight Parameters ($name)
    escaped = escaped.replace(
      /(\$[A-Za-z0-9_]+)/g,
      `<span style="color:#9cdcfe">$1</span>`
    );

    // 10. Restore string tokens
    for (let i = 0; i < stringTokens.length; i++) {
      escaped = escaped.replace(`${placeholderPrefix}${i}___`, stringTokens[i]);
    }

    if (commentPart) {
      escaped += `<span style="color:#6a9955;font-style:italic">${escapeHtml(commentPart)}</span>`;
    }

    return escaped;
  });

  return highlightedLines.join('\n');
}

export const CypherEditor: React.FC<CypherEditorProps> = ({
  value,
  onChange,
  placeholder = 'MATCH (n)\nWHERE ...\nRETURN n.path AS file, 1 AS line, n.name AS symbol, "" AS target, "message" AS message',
  readOnly = false,
  minHeight = 180,
  maxHeight = 360,
  onRunTest,
}) => {
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const preRef = useRef<HTMLPreElement>(null);
  const gutterRef = useRef<HTMLDivElement>(null);

  // Synchronize scrolling between textarea, highlighted pre layer, and line number gutter
  const handleScroll = useCallback(() => {
    if (!textareaRef.current) return;
    const top = textareaRef.current.scrollTop;
    const left = textareaRef.current.scrollLeft;

    if (preRef.current) {
      preRef.current.scrollTop = top;
      preRef.current.scrollLeft = left;
    }
    if (gutterRef.current) {
      gutterRef.current.scrollTop = top;
    }
  }, []);

  // Handle Tab key for indentation & Ctrl/Cmd+Enter for running test
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
        e.preventDefault();
        onRunTest?.();
        return;
      }

      if (e.key === 'Tab') {
        e.preventDefault();
        const textarea = textareaRef.current;
        if (!textarea) return;

        const start = textarea.selectionStart;
        const end = textarea.selectionEnd;

        if (e.shiftKey) {
          // Unindent
          const lines = value.split('\n');
          // Find line at cursor
          let pos = 0;
          let newContent = '';
          let newStart = start;
          let newEnd = end;

          for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const lineStart = pos;
            const lineEnd = pos + line.length;

            if (lineEnd >= start && lineStart <= end) {
              if (line.startsWith('  ')) {
                newContent += line.substring(2) + (i < lines.length - 1 ? '\n' : '');
                if (start >= lineStart + 2) newStart -= 2;
                if (end >= lineStart + 2) newEnd -= 2;
              } else if (line.startsWith(' ')) {
                newContent += line.substring(1) + (i < lines.length - 1 ? '\n' : '');
                if (start >= lineStart + 1) newStart -= 1;
                if (end >= lineStart + 1) newEnd -= 1;
              } else {
                newContent += line + (i < lines.length - 1 ? '\n' : '');
              }
            } else {
              newContent += line + (i < lines.length - 1 ? '\n' : '');
            }
            pos = lineEnd + 1;
          }
          onChange(newContent);
          requestAnimationFrame(() => {
            if (textareaRef.current) {
              textareaRef.current.selectionStart = Math.max(0, newStart);
              textareaRef.current.selectionEnd = Math.max(0, newEnd);
            }
          });
        } else {
          // Indent with 2 spaces
          const nextVal = value.substring(0, start) + '  ' + value.substring(end);
          onChange(nextVal);
          requestAnimationFrame(() => {
            if (textareaRef.current) {
              textareaRef.current.selectionStart = textareaRef.current.selectionEnd = start + 2;
            }
          });
        }
      }
    },
    [value, onChange, onRunTest]
  );

  const lines = useMemo(() => value.split('\n'), [value]);
  const lineCount = lines.length;

  const highlightedHtml = useMemo(() => highlightCypher(value), [value]);

  // Check required projection contract columns
  const contractStatus = useMemo(() => {
    const lower = value.toLowerCase();
    const returnIdx = lower.lastIndexOf('return');
    const returnClause = returnIdx >= 0 ? lower.substring(returnIdx) : lower;

    const hasFile = /\b(as\s+file|as\s+filepath|as\s+path)\b/.test(returnClause);
    const hasLine = /\b(as\s+line|as\s+startline|as\s+row)\b/.test(returnClause);
    const hasSymbol = /\b(as\s+symbol|as\s+name|as\s+source)\b/.test(returnClause);
    const hasTarget = /\b(as\s+target|as\s+destination)\b/.test(returnClause);
    const hasMessage = /\b(as\s+message|as\s+description|as\s+details)\b/.test(returnClause);

    return {
      hasFile,
      hasLine,
      hasSymbol,
      hasTarget,
      hasMessage,
      isFullyCompliant: hasFile && hasLine && hasSymbol && hasMessage,
    };
  }, [value]);

  const insertStandardProjection = useCallback(() => {
    const template = `\nRETURN \n  coalesce(n.path, n.file_path) AS file,\n  coalesce(n.start_line, 1) AS line,\n  coalesce(n.start_col, 1) AS column,\n  n.name AS symbol,\n  '' AS target,\n  'Invariant violation detected' AS message`;
    if (value.toLowerCase().includes('return')) {
      onChange(value + template);
    } else {
      onChange((value.trim() ? value + '\n' : 'MATCH (n)\nWHERE ...\n') + template);
    }
  }, [value, onChange]);

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        border: '1px solid var(--vscode-input-border, #3c3c3c)',
        borderRadius: '6px',
        backgroundColor: 'var(--vscode-editor-background, #1e1e1e)',
        overflow: 'hidden',
      }}
    >
      {/* Editor Body */}
      <div
        style={{
          position: 'relative',
          display: 'flex',
          minHeight,
          maxHeight,
          height: minHeight,
          overflow: 'hidden',
          fontFamily: 'var(--vscode-editor-font-family, Menlo, Monaco, Consolas, "Courier New", monospace)',
          fontSize: '12.5px',
          lineHeight: '20px',
        }}
      >
        {/* Line Numbers Gutter */}
        <div
          ref={gutterRef}
          style={{
            width: '42px',
            backgroundColor: 'rgba(0, 0, 0, 0.22)',
            borderRight: '1px solid var(--vscode-panel-border, rgba(255, 255, 255, 0.08))',
            color: 'var(--vscode-editorLineNumber-foreground, #6e7681)',
            padding: '8px 6px',
            textAlign: 'right',
            userSelect: 'none',
            overflow: 'hidden',
            flexShrink: 0,
            boxSizing: 'border-box',
          }}
        >
          {Array.from({ length: lineCount }).map((_, i) => (
            <div key={i} style={{ height: '20px', lineHeight: '20px' }}>
              {i + 1}
            </div>
          ))}
        </div>

        {/* Code Canvas Container */}
        <div style={{ position: 'relative', flex: 1, minWidth: 0, height: '100%', overflow: 'hidden' }}>
          {/* Syntax Highlight Backdrop Layer */}
          <pre
            ref={preRef}
            aria-hidden="true"
            dangerouslySetInnerHTML={{
              __html: highlightedHtml + (value.endsWith('\n') ? '<br/>' : ''),
            }}
            style={{
              position: 'absolute',
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              margin: 0,
              padding: '8px 12px',
              fontFamily: 'inherit',
              fontSize: 'inherit',
              lineHeight: 'inherit',
              whiteSpace: 'pre',
              wordWrap: 'normal',
              pointerEvents: 'none',
              overflow: 'hidden',
              boxSizing: 'border-box',
              color: 'var(--vscode-editor-foreground, #cccccc)',
              tabSize: 2,
            }}
          />

          {/* Transparent Input Textarea Layer */}
          <textarea
            ref={textareaRef}
            value={value}
            onChange={(e) => onChange(e.target.value)}
            onScroll={handleScroll}
            onKeyDown={handleKeyDown}
            readOnly={readOnly}
            placeholder={placeholder}
            spellCheck={false}
            autoCapitalize="off"
            autoCorrect="off"
            style={{
              position: 'absolute',
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              margin: 0,
              padding: '8px 12px',
              fontFamily: 'inherit',
              fontSize: 'inherit',
              lineHeight: 'inherit',
              whiteSpace: 'pre',
              wordWrap: 'normal',
              color: 'transparent',
              caretColor: 'var(--vscode-editorCursor-foreground, #ffffff)',
              backgroundColor: 'transparent',
              border: 'none',
              outline: 'none',
              resize: 'none',
              overflow: 'auto',
              boxSizing: 'border-box',
              tabSize: 2,
            }}
          />
        </div>
      </div>

      {/* Contract & Action Footer Bar */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '5px 10px',
          borderTop: '1px solid var(--vscode-panel-border, rgba(255, 255, 255, 0.08))',
          backgroundColor: 'rgba(0, 0, 0, 0.16)',
          fontSize: '11px',
          color: 'var(--vscode-descriptionForeground, #94a3b8)',
          gap: '8px',
          flexWrap: 'wrap',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', flexWrap: 'wrap' }}>
          <span style={{ fontWeight: 600, color: '#94a3b8' }}>Contract Projection:</span>
          <span
            style={{
              padding: '1px 5px',
              borderRadius: '3px',
              fontSize: '10px',
              fontWeight: 600,
              backgroundColor: contractStatus.hasFile ? 'rgba(16, 185, 129, 0.18)' : 'rgba(239, 68, 68, 0.18)',
              color: contractStatus.hasFile ? '#34d399' : '#f87171',
              border: `1px solid ${contractStatus.hasFile ? 'rgba(16, 185, 129, 0.3)' : 'rgba(239, 68, 68, 0.3)'}`,
            }}
          >
            {contractStatus.hasFile ? '✓ file' : '✗ file'}
          </span>
          <span
            style={{
              padding: '1px 5px',
              borderRadius: '3px',
              fontSize: '10px',
              fontWeight: 600,
              backgroundColor: contractStatus.hasLine ? 'rgba(16, 185, 129, 0.18)' : 'rgba(239, 68, 68, 0.18)',
              color: contractStatus.hasLine ? '#34d399' : '#f87171',
              border: `1px solid ${contractStatus.hasLine ? 'rgba(16, 185, 129, 0.3)' : 'rgba(239, 68, 68, 0.3)'}`,
            }}
          >
            {contractStatus.hasLine ? '✓ line' : '✗ line'}
          </span>
          <span
            style={{
              padding: '1px 5px',
              borderRadius: '3px',
              fontSize: '10px',
              fontWeight: 600,
              backgroundColor: contractStatus.hasSymbol ? 'rgba(16, 185, 129, 0.18)' : 'rgba(239, 68, 68, 0.18)',
              color: contractStatus.hasSymbol ? '#34d399' : '#f87171',
              border: `1px solid ${contractStatus.hasSymbol ? 'rgba(16, 185, 129, 0.3)' : 'rgba(239, 68, 68, 0.3)'}`,
            }}
          >
            {contractStatus.hasSymbol ? '✓ symbol' : '✗ symbol'}
          </span>
          <span
            style={{
              padding: '1px 5px',
              borderRadius: '3px',
              fontSize: '10px',
              fontWeight: 600,
              backgroundColor: contractStatus.hasTarget ? 'rgba(16, 185, 129, 0.18)' : 'rgba(148, 163, 184, 0.12)',
              color: contractStatus.hasTarget ? '#34d399' : '#94a3b8',
              border: `1px solid ${contractStatus.hasTarget ? 'rgba(16, 185, 129, 0.3)' : 'rgba(148, 163, 184, 0.2)'}`,
            }}
          >
            {contractStatus.hasTarget ? '✓ target' : '○ target'}
          </span>
          <span
            style={{
              padding: '1px 5px',
              borderRadius: '3px',
              fontSize: '10px',
              fontWeight: 600,
              backgroundColor: contractStatus.hasMessage ? 'rgba(16, 185, 129, 0.18)' : 'rgba(239, 68, 68, 0.18)',
              color: contractStatus.hasMessage ? '#34d399' : '#f87171',
              border: `1px solid ${contractStatus.hasMessage ? 'rgba(16, 185, 129, 0.3)' : 'rgba(239, 68, 68, 0.3)'}`,
            }}
          >
            {contractStatus.hasMessage ? '✓ message' : '✗ message'}
          </span>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          {!contractStatus.isFullyCompliant && (
            <button
              type="button"
              onClick={insertStandardProjection}
              style={{
                background: 'transparent',
                border: '1px solid var(--vscode-button-secondaryBackground, #3a3d41)',
                color: 'var(--vscode-button-secondaryForeground, #ffffff)',
                borderRadius: '3px',
                padding: '1px 7px',
                fontSize: '10.5px',
                cursor: 'pointer',
              }}
              title="Insert standard RETURN contract projection snippet"
            >
              + Insert Projection
            </button>
          )}
          <span style={{ fontSize: '10px', color: '#64748b' }}>
            Ctrl+Enter to test
          </span>
        </div>
      </div>
    </div>
  );
};
