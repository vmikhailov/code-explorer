import React from 'react';

export interface MastFooterProps {
  versionLabel?: string;
}

const DEFAULT_VERSION_LABEL = 'v1.101.1';

export const MastFooter: React.FC<MastFooterProps> = ({
  versionLabel = DEFAULT_VERSION_LABEL,
}) => (
  <footer className="site-footer">
    <div className="wrap footer-grid">
      <div className="footer-brand">
        <a href="/" className="wordmark-text">
          CodeExplorer
        </a>
        <p>
          Local code architecture graph for VS Code, MCP coding agents, and CI invariant checks. Free and open source under the MIT License.
        </p>
      </div>

      <ul className="footer-links">
        <li>
          <a
            href="https://github.com/vmikhailov/code-explorer"
            target="_blank"
            rel="noopener"
          >
            GitHub
          </a>
        </li>
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
          <a href="/benchmarks">Benchmarks</a>
        </li>
        <li>
          <a href="/ontology">Graph ontology</a>
        </li>
        <li>
          <a href="/architectures">Architecture gate packs</a>
        </li>
        <li>
          <a
            href="https://www.nuget.org/packages/codeexplorer.cli"
            target="_blank"
            rel="noopener"
          >
            NuGet (codeexplorer.cli)
          </a>
        </li>
        <li>
          <a
            href="https://github.com/vmikhailov/code-explorer/releases"
            target="_blank"
            rel="noopener"
          >
            Release notes
          </a>
        </li>
        <li>
          <a href="/privacy">Privacy Policy</a>
        </li>
      </ul>

      <p className="footer-version" id="footerCeVersion">
        {versionLabel}
      </p>
    </div>
  </footer>
);
