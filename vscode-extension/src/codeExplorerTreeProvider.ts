import * as vscode from 'vscode';
import * as http from 'http';
import * as path from 'path';
import { ProcessManager, ServerInfo } from './processManager';

export interface MetadataDto {
  nodeCounts: Record<string, number>;
  relationshipCounts: Record<string, number>;
  totalNodes: number;
  totalEdges: number;
}

export interface NodeDto {
  id: string;
  name: string;
  kind: string;
  filePath?: string;
  lineStart?: number;
  lineEnd?: number;
  properties?: Record<string, string>;
}

export interface NodesResponseDto {
  kind: string;
  nodes: NodeDto[];
  total: number;
  offset: number;
  limit: number;
}

export type TreeItemType =
  | 'root-views'
  | 'root-metadata'
  | 'root-nodes'
  | 'view-item'
  | 'metadata-stat'
  | 'metadata-rels-group'
  | 'metadata-rel-item'
  | 'node-category'
  | 'node-item'
  | 'load-more-item'
  | 'empty-notice'
  | 'init-action';

export class CodeExplorerTreeItem extends vscode.TreeItem {
  constructor(
    public readonly itemType: TreeItemType,
    label: string,
    collapsibleState: vscode.TreeItemCollapsibleState,
    public readonly data?: any
  ) {
    super(label, collapsibleState);
  }
}

async function fetchJson<T>(urlStr: string): Promise<T> {
  if (typeof fetch === 'function') {
    const res = await fetch(urlStr);
    if (!res.ok) {
      throw new Error(`HTTP ${res.status}: ${res.statusText}`);
    }
    return (await res.json()) as T;
  }

  return new Promise<T>((resolve, reject) => {
    http
      .get(urlStr, (res) => {
        let body = '';
        res.on('data', (chunk) => {
          body += chunk;
        });
        res.on('end', () => {
          try {
            resolve(JSON.parse(body));
          } catch (e) {
            reject(e);
          }
        });
      })
      .on('error', reject);
  });
}

export class CodeExplorerTreeDataProvider implements vscode.TreeDataProvider<CodeExplorerTreeItem> {
  private _onDidChangeTreeData: vscode.EventEmitter<CodeExplorerTreeItem | undefined | null | void> =
    new vscode.EventEmitter<CodeExplorerTreeItem | undefined | null | void>();
  readonly onDidChangeTreeData: vscode.Event<CodeExplorerTreeItem | undefined | null | void> =
    this._onDidChangeTreeData.event;

  private categoryLimits = new Map<string, number>();
  private defaultPageSize = 50;

  constructor(
    private readonly processManager: ProcessManager,
    private readonly getWorkspaceRoot: () => string | undefined,
    private readonly outputChannel: vscode.OutputChannel
  ) {
    // Automatically re-render tree when server becomes ready or stops
    this.processManager.onDidServerStart(() => {
      this.refresh();
    });
    this.processManager.onDidServerStop(() => {
      this.refresh();
    });
  }

  refresh(): void {
    this._onDidChangeTreeData.fire();
  }

  loadMore(kind: string): void {
    const current = this.categoryLimits.get(kind) || this.defaultPageSize;
    this.categoryLimits.set(kind, current + this.defaultPageSize);
    this.refresh();
  }

  private async getServerInfo(): Promise<ServerInfo | null> {
    const root = this.getWorkspaceRoot();
    if (!root) return null;
    if (!this.processManager.hasWorkspace(root)) return null;

    // Check if server is already running and ready
    const existing = this.processManager.getServerInfo();
    if (existing) return existing;

    try {
      return await this.processManager.ensureServerStarted(root);
    } catch (e: any) {
      this.outputChannel.appendLine(`[TreeProvider] Server start error: ${e.message}`);
      return null;
    }
  }

  getTreeItem(element: CodeExplorerTreeItem): vscode.TreeItem {
    return element;
  }

  async getChildren(element?: CodeExplorerTreeItem): Promise<CodeExplorerTreeItem[]> {
    if (!element) {
      const root = this.getWorkspaceRoot();
      if (!root) {
        return [];
      }

      if (!this.processManager.hasWorkspace(root)) {
        const initItem = new CodeExplorerTreeItem(
          'init-action',
          'Initialize & Scan Workspace',
          vscode.TreeItemCollapsibleState.None
        );
        initItem.description = 'Build graph database';
        initItem.iconPath = new vscode.ThemeIcon('rocket');
        initItem.tooltip = 'Click to run `ce init` and `ce index` to scan this project';
        initItem.command = {
          command: 'codeExplorer.initAndScan',
          title: 'Initialize & Scan Workspace',
        };

        const noticeItem = new CodeExplorerTreeItem(
          'empty-notice',
          'No .codeexplorer directory found',
          vscode.TreeItemCollapsibleState.None
        );
        noticeItem.description = 'Graph not yet generated';
        noticeItem.iconPath = new vscode.ThemeIcon('info');
        noticeItem.tooltip = 'Run Initialize & Scan Workspace to parse ASTs and build the architecture graph.';

        return [initItem, noticeItem];
      }

      // Root level when .codeexplorer exists
      const viewsRoot = new CodeExplorerTreeItem(
        'root-views',
        'Views',
        vscode.TreeItemCollapsibleState.Expanded
      );
      viewsRoot.iconPath = new vscode.ThemeIcon('layout');
      viewsRoot.tooltip = 'Architecture and dependency diagram views';

      const metadataRoot = new CodeExplorerTreeItem(
        'root-metadata',
        'Metadata',
        vscode.TreeItemCollapsibleState.Collapsed
      );
      metadataRoot.iconPath = new vscode.ThemeIcon('graph');
      metadataRoot.tooltip = 'Graph statistics and entity counts';

      const nodesRoot = new CodeExplorerTreeItem(
        'root-nodes',
        'Nodes',
        vscode.TreeItemCollapsibleState.Collapsed
      );
      nodesRoot.iconPath = new vscode.ThemeIcon('symbol-structure');
      nodesRoot.tooltip = 'Browse nodes by kind with paging';

      return [viewsRoot, metadataRoot, nodesRoot];
    }

    if (element.itemType === 'root-views') {
      return this.getViewItems();
    }

    if (element.itemType === 'root-metadata') {
      return this.getMetadataItems();
    }

    if (element.itemType === 'metadata-rels-group') {
      return this.getRelationshipDetails(element.data?.relationshipCounts);
    }

    if (element.itemType === 'root-nodes') {
      return this.getNodeCategories();
    }

    if (element.itemType === 'node-category') {
      return this.getNodesForCategory(element.data?.kind);
    }

    return [];
  }

  private getViewItems(): CodeExplorerTreeItem[] {
    const views: { mode: string; label: string; desc: string; icon: string }[] = [
      {
        mode: 'c1',
        label: 'C1: System Context & Boundaries',
        desc: 'Ingress, System Boundary, Egress',
        icon: 'globe',
      },
      {
        mode: 'flow',
        label: 'C2: Project Flow & Dependencies',
        desc: 'Project interaction flows',
        icon: 'git-compare',
      },
      {
        mode: 'layers',
        label: 'C3: System Layers & Tiers',
        desc: 'Domain, App, Infra, Presentation',
        icon: 'layers',
      },
      {
        mode: 'semantic',
        label: 'Domain Bounded Contexts',
        desc: 'Microservices & Domain Map',
        icon: 'symbol-namespace',
      },
      {
        mode: 'full',
        label: 'Physical Dependency Graph',
        desc: 'All projects & physical edges',
        icon: 'type-hierarchy-sub',
      },
    ];

    return views.map((v) => {
      const item = new CodeExplorerTreeItem(
        'view-item',
        v.label,
        vscode.TreeItemCollapsibleState.None,
        { viewMode: v.mode }
      );
      item.description = v.desc;
      item.iconPath = new vscode.ThemeIcon(v.icon);
      item.command = {
        command: 'codeExplorer.openView',
        title: `Open ${v.label}`,
        arguments: [v.mode],
      };
      return item;
    });
  }

  private async getMetadataItems(): Promise<CodeExplorerTreeItem[]> {
    const root = this.getWorkspaceRoot();
    if (!root) {
      const item = new CodeExplorerTreeItem('metadata-stat', 'No folder open', vscode.TreeItemCollapsibleState.None);
      item.iconPath = new vscode.ThemeIcon('info');
      item.tooltip = 'Open a workspace folder to view architecture graph';
      return [item];
    }

    if (!this.processManager.hasWorkspace(root)) {
      const item = new CodeExplorerTreeItem(
        'metadata-stat',
        'Initialize & Scan Workspace',
        vscode.TreeItemCollapsibleState.None
      );
      item.description = '.codeexplorer not found';
      item.iconPath = new vscode.ThemeIcon('rocket');
      item.tooltip = 'Click to initialize .codeexplorer and scan codebase';
      item.command = {
        command: 'codeExplorer.initAndScan',
        title: 'Initialize & Scan Workspace',
      };
      return [item];
    }

    const serverInfo = await this.getServerInfo();
    if (!serverInfo) {
      const isStarting = this.processManager.isStarting();
      const item = new CodeExplorerTreeItem(
        'metadata-stat',
        isStarting ? 'Starting server...' : 'Start Server',
        vscode.TreeItemCollapsibleState.None
      );
      item.iconPath = new vscode.ThemeIcon(isStarting ? 'loading~spin' : 'play');
      item.description = isStarting ? 'warming up' : 'click to run';
      item.tooltip = isStarting
        ? 'CodeExplorer server is starting in the background...'
        : 'Click to start CodeExplorer server';
      item.command = {
        command: 'codeExplorer.showGraph',
        title: 'Start Server',
      };
      return [item];
    }

    try {
      const meta = await fetchJson<MetadataDto>(`${serverInfo.httpUrl}/api/metadata`);
      const counts = meta.nodeCounts || {};
      const relCounts = meta.relationshipCounts || {};

      const items: CodeExplorerTreeItem[] = [];

      const kindSpecs: { kind: string; label: string; icon: string }[] = [
        { kind: 'Project', label: 'Projects', icon: 'project' },
        { kind: 'Endpoint', label: 'Endpoints', icon: 'radio-tower' },
        { kind: 'Database', label: 'Databases', icon: 'database' },
        { kind: 'ExternalService', label: 'External Services', icon: 'cloud' },
        { kind: 'Table', label: 'Tables', icon: 'table' },
        { kind: 'Type', label: 'Types', icon: 'symbol-class' },
        { kind: 'Function', label: 'Functions', icon: 'symbol-method' },
        { kind: 'Query', label: 'Queries', icon: 'search' },
        { kind: 'Package', label: 'Packages', icon: 'package' },
        { kind: 'File', label: 'Files', icon: 'file-code' },
      ];

      for (const spec of kindSpecs) {
        const count = counts[spec.kind] || 0;
        if (count > 0 || ['Project', 'Endpoint', 'Database', 'ExternalService', 'Table'].includes(spec.kind)) {
          const item = new CodeExplorerTreeItem(
            'metadata-stat',
            spec.label,
            vscode.TreeItemCollapsibleState.None,
            { kind: spec.kind, count }
          );
          item.description = `${count.toLocaleString()}`;
          item.iconPath = new vscode.ThemeIcon(spec.icon);
          item.tooltip = `${count.toLocaleString()} ${spec.label} indexed in graph`;
          items.push(item);
        }
      }

      // Relationships Group
      const relItem = new CodeExplorerTreeItem(
        'metadata-rels-group',
        'Relationships',
        vscode.TreeItemCollapsibleState.Collapsed,
        { relationshipCounts: relCounts, totalEdges: meta.totalEdges }
      );
      relItem.description = `${(meta.totalEdges || 0).toLocaleString()}`;
      relItem.iconPath = new vscode.ThemeIcon('references');
      relItem.tooltip = `${(meta.totalEdges || 0).toLocaleString()} total relationships`;
      items.push(relItem);

      return items;
    } catch (err: any) {
      const errItem = new CodeExplorerTreeItem('metadata-stat', `Error: ${err.message}`, vscode.TreeItemCollapsibleState.None);
      errItem.iconPath = new vscode.ThemeIcon('error');
      return [errItem];
    }
  }

  private getRelationshipDetails(relCounts?: Record<string, number>): CodeExplorerTreeItem[] {
    if (!relCounts) return [];
    return Object.entries(relCounts)
      .sort((a, b) => b[1] - a[1])
      .map(([relType, count]) => {
        const item = new CodeExplorerTreeItem(
          'metadata-rel-item',
          relType,
          vscode.TreeItemCollapsibleState.None
        );
        item.description = `${count.toLocaleString()}`;
        item.iconPath = new vscode.ThemeIcon('arrow-right');
        return item;
      });
  }

  private async getNodeCategories(): Promise<CodeExplorerTreeItem[]> {
    const serverInfo = await this.getServerInfo();
    let counts: Record<string, number> = {};
    if (serverInfo) {
      try {
        const meta = await fetchJson<MetadataDto>(`${serverInfo.httpUrl}/api/metadata`);
        counts = meta.nodeCounts || {};
      } catch {}
    }

    const categories: { kind: string; label: string; icon: string }[] = [
      { kind: 'Project', label: 'Projects', icon: 'project' },
      { kind: 'Endpoint', label: 'Endpoints', icon: 'radio-tower' },
      { kind: 'Database', label: 'Databases', icon: 'database' },
      { kind: 'ExternalService', label: 'External Services', icon: 'cloud' },
      { kind: 'Table', label: 'Tables', icon: 'table' },
      { kind: 'Type', label: 'Types', icon: 'symbol-class' },
      { kind: 'Function', label: 'Functions', icon: 'symbol-method' },
      { kind: 'Query', label: 'Queries', icon: 'search' },
      { kind: 'Package', label: 'Packages', icon: 'package' },
    ];

    return categories.map((cat) => {
      const count = counts[cat.kind];
      const item = new CodeExplorerTreeItem(
        'node-category',
        cat.label,
        vscode.TreeItemCollapsibleState.Collapsed,
        { kind: cat.kind }
      );
      item.description = count !== undefined ? `${count.toLocaleString()}` : '';
      item.iconPath = new vscode.ThemeIcon(cat.icon);
      item.tooltip = `Browse ${cat.label}`;
      return item;
    });
  }

  private async getNodesForCategory(kind?: string): Promise<CodeExplorerTreeItem[]> {
    if (!kind) return [];

    const root = this.getWorkspaceRoot();
    if (!root || !this.processManager.hasWorkspace(root)) {
      const item = new CodeExplorerTreeItem(
        'node-item',
        'Initialize & Scan Workspace',
        vscode.TreeItemCollapsibleState.None
      );
      item.description = '.codeexplorer not found';
      item.iconPath = new vscode.ThemeIcon('rocket');
      item.command = {
        command: 'codeExplorer.initAndScan',
        title: 'Initialize & Scan Workspace',
      };
      return [item];
    }

    const serverInfo = await this.getServerInfo();
    if (!serverInfo) {
      const isStarting = this.processManager.isStarting();
      const item = new CodeExplorerTreeItem(
        'node-item',
        isStarting ? 'Starting server...' : 'Start Server',
        vscode.TreeItemCollapsibleState.None
      );
      item.iconPath = new vscode.ThemeIcon(isStarting ? 'loading~spin' : 'play');
      item.description = isStarting ? 'warming up' : 'click to run';
      item.command = {
        command: 'codeExplorer.showGraph',
        title: 'Start Server',
      };
      return [item];
    }

    const limit = this.categoryLimits.get(kind) || this.defaultPageSize;

    try {
      const res = await fetchJson<NodesResponseDto>(
        `${serverInfo.httpUrl}/api/nodes?kind=${encodeURIComponent(kind)}&offset=0&limit=${limit}`
      );

      const items: CodeExplorerTreeItem[] = [];

      for (const node of res.nodes) {
        const item = new CodeExplorerTreeItem(
          'node-item',
          node.name || node.id,
          vscode.TreeItemCollapsibleState.None,
          node
        );

        item.tooltip = `ID: ${node.id}\nKind: ${node.kind}${node.filePath ? `\nFile: ${node.filePath}` : ''}${node.lineStart ? `:${node.lineStart}` : ''}`;

        // Description
        if (node.properties?.method) {
          item.description = node.properties.method;
        } else if (node.properties?.framework) {
          item.description = node.properties.framework;
        } else if (node.filePath) {
          const baseName = path.basename(node.filePath);
          item.description = node.lineStart ? `${baseName}:${node.lineStart}` : baseName;
        }

        // Icon
        item.iconPath = this.getIconForKind(node.kind);

        // Click command: focus node in graph
        item.command = {
          command: 'codeExplorer.focusNode',
          title: 'Focus in Graph',
          arguments: [node.id, node.kind],
        };

        // If file exists, allow context menu or inline action to open source
        if (node.filePath) {
          item.contextValue = 'node-with-source';
        } else {
          item.contextValue = 'node';
        }

        items.push(item);
      }

      // Check if more items exist
      if (res.total > res.nodes.length) {
        const moreItem = new CodeExplorerTreeItem(
          'load-more-item',
          `▶ Load ${Math.min(this.defaultPageSize, res.total - res.nodes.length)} more...`,
          vscode.TreeItemCollapsibleState.None,
          { kind }
        );
        moreItem.description = `Showing ${res.nodes.length} of ${res.total}`;
        moreItem.iconPath = new vscode.ThemeIcon('chevron-down');
        moreItem.tooltip = `Click to load the next ${this.defaultPageSize} items`;
        moreItem.command = {
          command: 'codeExplorer.loadMoreNodes',
          title: 'Load More Nodes',
          arguments: [kind],
        };
        items.push(moreItem);
      }

      return items;
    } catch (err: any) {
      const errItem = new CodeExplorerTreeItem('node-item', `Error: ${err.message}`, vscode.TreeItemCollapsibleState.None);
      errItem.iconPath = new vscode.ThemeIcon('error');
      return [errItem];
    }
  }

  private getIconForKind(kind: string): vscode.ThemeIcon {
    switch (kind) {
      case 'Project':
        return new vscode.ThemeIcon('project');
      case 'Endpoint':
        return new vscode.ThemeIcon('radio-tower');
      case 'Database':
        return new vscode.ThemeIcon('database');
      case 'ExternalService':
        return new vscode.ThemeIcon('cloud');
      case 'Table':
        return new vscode.ThemeIcon('table');
      case 'Type':
        return new vscode.ThemeIcon('symbol-class');
      case 'Function':
        return new vscode.ThemeIcon('symbol-method');
      case 'Query':
        return new vscode.ThemeIcon('search');
      case 'Package':
        return new vscode.ThemeIcon('package');
      case 'File':
        return new vscode.ThemeIcon('file-code');
      default:
        return new vscode.ThemeIcon('circle-outline');
    }
  }
}
