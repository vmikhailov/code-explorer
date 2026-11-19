import React from 'react';

export interface MastFooterProps {
  versionLabel?: string;
}

const DEFAULT_VERSION_LABEL = 'CodeExplorer CLI & Extension v1.90.12';

export const MastFooter: React.FC<MastFooterProps> = ({
  versionLabel = DEFAULT_VERSION_LABEL,
}) => (
  <footer className="mast-footer">
    <div className="wrap">
      <div className="mast-top">
        <div className="mast-col">
          <a href="/" className="brand-lockup">
            <span className="brand-mark">ce</span>
            <span>CodeExplorer</span>
          </a>
          <p className="panel-copy">
            Live architecture maps in VS Code, graph memory for AI agents via MCP, and deterministic CI gates.
          </p>
        </div>

        <div className="mast-col">
          <span className="mast-heading">Distribution</span>
          <ul className="mast-list">
            <li>
              <a
                href="https://marketplace.visualstudio.com/items?itemName=vmikhailov.code-explorer-vscode"
                target="_blank"
                rel="noopener"
              >
                VS Code Marketplace
              </a>
            </li>
            <li>
              <a
                href="https://open-vsx.org/extension/vmikhailov/code-explorer-vscode"
                target="_blank"
                rel="noopener"
              >
                Open VSX Registry
              </a>
            </li>
            <li>
              <a href="https://github.com/vmikhailov/homebrew-tap" target="_blank" rel="noopener">
                Homebrew Tap
              </a>
            </li>
            <li>
              <a href="https://www.nuget.org/packages/codeexplorer.cli" target="_blank" rel="noopener">
                NuGet Global Tool
              </a>
            </li>
          </ul>
        </div>

        <div className="mast-col">
          <span className="mast-heading">Architecture &amp; Docs</span>
          <ul className="mast-list">
            <li>
              <a href="/architectures">Architecture Encyclopedia</a>
            </li>
            <li>
              <a href="/ontology">Graph Ontology Map</a>
            </li>
            <li>
              <a href="/benchmarks">Competitor Benchmarks</a>
            </li>
            <li>
              <a
                href="https://passport.code-explorer.app"
                target="_blank"
                rel="noopener"
                className="tone-accent"
              >
                CodePassport
              </a>
            </li>
            <li>
              <a href="/presentation.html" target="_blank" rel="noopener">
                Interactive Presentation
              </a>
            </li>
            <li>
              <a href="/llms.txt" target="_blank" rel="noopener">
                llms.txt for Agents
              </a>
            </li>
          </ul>
        </div>

        <div className="mast-col">
          <span className="mast-heading">Project &amp; Legal</span>
          <ul className="mast-list">
            <li>
              <a href="https://github.com/vmikhailov/code-explorer" target="_blank" rel="noopener">
                GitHub Repository
              </a>
            </li>
            <li>
              <a
                href="https://github.com/vmikhailov/code-explorer/releases"
                target="_blank"
                rel="noopener"
              >
                Release Notes
              </a>
            </li>
            <li>
              <a
                href="https://github.com/vmikhailov/code-explorer/issues"
                target="_blank"
                rel="noopener"
              >
                Issue Tracker
              </a>
            </li>
            <li>
              <a
                href="https://github.com/vmikhailov/code-explorer/blob/main/LICENSE"
                target="_blank"
                rel="noopener"
              >
                MIT License
              </a>
            </li>
            <li>
              <a href="/privacy">Privacy Policy</a>
            </li>
          </ul>
        </div>
      </div>

      <div className="mast-bottom">
        <span>Built by Viacheslav Mikhailov, 100% local execution</span>
        <span className="measured" id="footerCeVersion">
          {versionLabel}
        </span>
      </div>
    </div>
  </footer>
);
