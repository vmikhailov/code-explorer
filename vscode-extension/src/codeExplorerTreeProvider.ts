import * as vscode from 'vscode';
import * as http from 'http';
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
  displayName?: string;
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
  | 'root-diagrams'
  | 'root-layers'
  | 'root-metadata'
  | 'root-management'
  | 'diagram-item'
  | 'layer-group'
  | 'node-category'
  | 'rel-category'
  | 'metadata-stat'
  | 'management-item'
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

  constructor(
    private readonly processManager: ProcessManager,
    private readonly getWorkspaceRoot: () => string | undefined,
    private readonly outputChannel: vscode.OutputChannel
  ) {
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

  private async getServerInfo(): Promise<ServerInfo | null> {
    const root = this.getWorkspaceRoot();
    if (!root) return null;
    if (!this.processManager.hasWorkspace(root)) return null;

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
    const root = this.getWorkspaceRoot();
    if (!root) {
      return [];
    }

    if (!element) {
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

      // Root level sections:
      // 1. Architecture Diagrams
      const diagramsRoot = new CodeExplorerTreeItem(
        'root-diagrams',
        'Architecture Diagrams',
        vscode.TreeItemCollapsibleState.Expanded
      );
      diagramsRoot.iconPath = new vscode.ThemeIcon('layout');
      diagramsRoot.tooltip = 'High-level C1 & C2 architectural visualizers and Mermaid diagrams';

      // 2. Graph Layers (Ontology Layers 1 - 5)
      const layersRoot = new CodeExplorerTreeItem(
        'root-layers',
        'Graph Layers (Ontology 1 - 5)',
        vscode.TreeItemCollapsibleState.Expanded
      );
      layersRoot.iconPath = new vscode.ThemeIcon('layers');
      layersRoot.tooltip = 'Decoupled 5-layer ontology graph model';

      // 3. Metadata & Health
      const serverInfo = this.processManager.getServerInfo();
      const isStarting = this.processManager.isStarting();

      const metadataRoot = new CodeExplorerTreeItem(
        'root-metadata',
        'Metadata & Statistics',
        vscode.TreeItemCollapsibleState.Collapsed
      );
      metadataRoot.iconPath = new vscode.ThemeIcon('graph');
      metadataRoot.description = serverInfo ? 'Connected' : isStarting ? 'Connecting...' : 'Offline';
      metadataRoot.tooltip = 'Graph statistics, connection status, and database summary';

      // 4. Management (Rescan, Rebuild, Server control)
      const managementRoot = new CodeExplorerTreeItem(
        'root-management',
        'Management',
        vscode.TreeItemCollapsibleState.Expanded
      );
      managementRoot.iconPath = new vscode.ThemeIcon('tools');
      managementRoot.tooltip = 'Workspace scanning and graph lifecycle management';

      return [diagramsRoot, layersRoot, metadataRoot, managementRoot];
    }

    if (element.itemType === 'root-diagrams') {
      return this.getDiagramItems();
    }

    if (element.itemType === 'root-layers') {
      return this.getGraphLayerGroups();
    }

    if (element.itemType === 'layer-group') {
      return this.getLayerCategoryItems(element.data?.layerId, element.data?.title);
    }

    if (element.itemType === 'root-metadata') {
      return this.getMetadataItems();
    }

    if (element.itemType === 'root-management') {
      return this.getManagementItems();
    }

    return [];
  }

  private getDiagramItems(): CodeExplorerTreeItem[] {
    const diagrams = [
      {
        mode: 'layers',
        label: 'Project Architecture Diagram',
        desc: 'Tiered projects & databases',
        icon: 'layers',
        tooltip: 'Project Architecture Diagram — Tiered system view (Presentation, Application, Domain, Infrastructure)',
      },
      {
        mode: 'c1',
        label: 'C1: System Context',
        desc: 'Semantic boundaries & external APIs',
        icon: 'globe',
        tooltip: 'C1: System Context — Ingress endpoints, system boundary, databases, and egress topics',
      },
      {
        mode: 'flow',
        label: 'C2: Project Flow',
        desc: 'Focused dependency & call flow',
        icon: 'git-compare',
        tooltip: 'C2: Project Flow — Topological dependency columns, call chains, and message flows',
      },
      {
        mode: 'full',
        label: 'Project Dependency Graph',
        desc: 'Interactive full project graph',
        icon: 'type-hierarchy-sub',
        tooltip: 'Physical Dependency Graph — Complete workspace graph of all projects and dependencies',
      },
      {
        mode: 'semantic',
        label: 'Domain Microservice Map',
        desc: 'Bounded contexts & clusters',
        icon: 'symbol-namespace',
        tooltip: 'Domain Architecture — Microservice domains and bounded contexts',
      },
      {
        mode: 'mermaid',
        label: 'Mermaid Architecture Diagram',
        desc: 'Mermaid flowchart renderer',
        icon: 'graph',
        tooltip: 'Mermaid Architecture Diagram — Interactive SVG diagram with exportable Markdown',
      },
    ];

    return diagrams.map((d) => {
      const item = new CodeExplorerTreeItem(
        'diagram-item',
        d.label,
        vscode.TreeItemCollapsibleState.None,
        { viewMode: d.mode }
      );
      item.description = d.desc;
      item.iconPath = new vscode.ThemeIcon(d.icon);
      item.tooltip = d.tooltip;
      item.command = {
        command: 'codeExplorer.openView',
        title: `Open ${d.label}`,
        arguments: [d.mode],
      };
      return item;
    });
  }

  private async getGraphLayerGroups(): Promise<CodeExplorerTreeItem[]> {
    const serverInfo = await this.getServerInfo();
    let meta: MetadataDto | null = null;
    if (serverInfo) {
      try {
        meta = await fetchJson<MetadataDto>(`${serverInfo.httpUrl}/api/metadata`);
      } catch {}
    }

    const counts = meta?.nodeCounts || {};
    const relCounts = meta?.relationshipCounts || {};

    // Sum nodes per layer
    const l1Count = (counts['File'] || 0) + (counts['Folder'] || 0) + (counts['GitSettings'] || 0);
    const l2Count = (counts['Project'] || 0) + (counts['Package'] || 0);
    const l3Count = (counts['Type'] || 0) + (counts['Function'] || 0) + (counts['Member'] || 0);
    const l4Count =
      (counts['Endpoint'] || 0) +
      (counts['Database'] || 0) +
      (counts['Table'] || 0) +
      (counts['Topic'] || 0) +
      (counts['EntryPoint'] || 0) +
      (counts['ExternalService'] || 0) +
      (counts['CloudService'] || 0) +
      (counts['ApiInUse'] || 0) +
      (counts['Query'] || 0);
    const l5Count = meta?.totalEdges || Object.values(relCounts).reduce((acc, c) => acc + c, 0);

    const layers = [
      {
        layerId: 1,
        title: 'Layer 1: Physical Topology',
        desc: `${l1Count.toLocaleString()} nodes`,
        icon: 'folder-library',
        tooltip: 'Layer 1: Files, Folders, and Git configuration',
      },
      {
        layerId: 2,
        title: 'Layer 2: Project Boundary',
        desc: `${l2Count.toLocaleString()} nodes`,
        icon: 'project',
        tooltip: 'Layer 2: Logical compilation scopes, projects, and external packages',
      },
      {
        layerId: 3,
        title: 'Layer 3: Syntactic AST',
        desc: `${l3Count.toLocaleString()} nodes`,
        icon: 'symbol-structure',
        tooltip: 'Layer 3: Abstract Syntax Tree declarations (Types, Methods, Fields)',
      },
      {
        layerId: 4,
        title: 'Layer 4: Semantic Runtime',
        desc: `${l4Count.toLocaleString()} nodes`,
        icon: 'radio-tower',
        tooltip: 'Layer 4: Runtime architecture (Endpoints, Databases, Topics, EntryPoints, External Services)',
      },
      {
        layerId: 5,
        title: 'Layer 5: System Bindings',
        desc: `${l5Count.toLocaleString()} edges`,
        icon: 'references',
        tooltip: 'Layer 5: Cross-project late-bound relationships (CALLS, IMPLEMENTS, USES_DB, INTEGRATES_WITH)',
      },
    ];

    return layers.map((l) => {
      const item = new CodeExplorerTreeItem(
        'layer-group',
        l.title,
        vscode.TreeItemCollapsibleState.Collapsed,
        { layerId: l.layerId, title: l.title }
      );
      item.description = l.desc;
      item.iconPath = new vscode.ThemeIcon(l.icon);
      item.tooltip = l.tooltip;
      return item;
    });
  }

  private async getLayerCategoryItems(layerId?: number, layerTitle?: string): Promise<CodeExplorerTreeItem[]> {
    if (!layerId) return [];

    const serverInfo = await this.getServerInfo();
    let meta: MetadataDto | null = null;
    if (serverInfo) {
      try {
        meta = await fetchJson<MetadataDto>(`${serverInfo.httpUrl}/api/metadata`);
      } catch {}
    }

    const counts = meta?.nodeCounts || {};
    const relCounts = meta?.relationshipCounts || {};

    const items: CodeExplorerTreeItem[] = [];

    const createNodeCatItem = (kind: string, label: string, icon: string) => {
      const count = counts[kind] || 0;
      const item = new CodeExplorerTreeItem(
        'node-category',
        label,
        vscode.TreeItemCollapsibleState.None,
        { kind, layerTitle: layerTitle || `Layer ${layerId}` }
      );
      item.description = `${count.toLocaleString()}`;
      item.iconPath = new vscode.ThemeIcon(icon);
      item.tooltip = `Click to browse all ${count.toLocaleString()} ${label} in central grid`;
      item.command = {
        command: 'codeExplorer.openNodeGrid',
        title: `Browse ${label} in Grid`,
        arguments: [kind, layerTitle || `Layer ${layerId}`],
      };
      return item;
    };

    switch (layerId) {
      case 1:
        items.push(createNodeCatItem('File', 'Files', 'file-code'));
        items.push(createNodeCatItem('Folder', 'Folders', 'folder'));
        items.push(createNodeCatItem('GitSettings', 'Git Settings', 'git-commit'));
        break;

      case 2:
        items.push(createNodeCatItem('Project', 'Projects', 'project'));
        items.push(createNodeCatItem('Package', 'Packages', 'package'));
        break;

      case 3:
        items.push(createNodeCatItem('Type', 'Types (Classes, Interfaces)', 'symbol-class'));
        items.push(createNodeCatItem('Function', 'Functions & Methods', 'symbol-method'));
        items.push(createNodeCatItem('Member', 'Members & Fields', 'symbol-field'));
        break;

      case 4:
        items.push(createNodeCatItem('Endpoint', 'HTTP Endpoints', 'radio-tower'));
        items.push(createNodeCatItem('Database', 'Databases', 'database'));
        items.push(createNodeCatItem('Table', 'Database Tables', 'table'));
        items.push(createNodeCatItem('Topic', 'Message Topics & Queues', 'mail'));
        items.push(createNodeCatItem('EntryPoint', 'Execution EntryPoints', 'sign-in'));
        items.push(createNodeCatItem('ExternalService', 'External Services (Egress)', 'cloud'));
        items.push(createNodeCatItem('CloudService', 'Cloud Services', 'server'));
        items.push(createNodeCatItem('ApiInUse', 'APIs in Use', 'plug'));
        items.push(createNodeCatItem('Query', 'SQL Queries', 'search'));
        break;

      case 5: {
        const topRels = [
          'CALLS',
          'DEPENDS_ON',
          'EXPOSED_BY',
          'TRIGGERS',
          'QUERIED_BY',
          'PUBLISHED_BY',
          'SUBSCRIBED_BY',
          'INTEGRATES_WITH',
          'USES_DB',
          'IMPLEMENTS',
          'INHERITS_FROM',
          'USES_TYPE',
        ];

        for (const rel of topRels) {
          const count = relCounts[rel] || 0;
          if (count > 0 || ['CALLS', 'DEPENDS_ON', 'INTEGRATES_WITH', 'USES_DB'].includes(rel)) {
            const item = new CodeExplorerTreeItem(
              'rel-category',
              rel,
              vscode.TreeItemCollapsibleState.None,
              { rel, layerTitle: layerTitle || 'Layer 5: System Bindings' }
            );
            item.description = `${count.toLocaleString()}`;
            item.iconPath = new vscode.ThemeIcon('arrow-right');
            item.tooltip = `${count.toLocaleString()} ${rel} relationships`;
            item.command = {
              command: 'codeExplorer.openNodeGrid',
              title: `Browse ${rel} in Grid`,
              arguments: [rel, 'Layer 5: System Bindings'],
            };
            items.push(item);
          }
        }
        break;
      }
    }

    return items;
  }

  private async getMetadataItems(): Promise<CodeExplorerTreeItem[]> {
    const serverInfo = await this.getServerInfo();
    if (!serverInfo) {
      const isStarting = this.processManager.isStarting();
      const statusItem = new CodeExplorerTreeItem(
        'metadata-stat',
        'Connection Status',
        vscode.TreeItemCollapsibleState.None
      );
      statusItem.description = isStarting ? 'Connecting...' : 'Offline (Click to Start)';
      statusItem.iconPath = new vscode.ThemeIcon(isStarting ? 'loading~spin' : 'circle-slash');
      statusItem.command = {
        command: 'codeExplorer.showGraph',
        title: 'Start Server',
      };
      return [statusItem];
    }

    try {
      const meta = await fetchJson<MetadataDto>(`${serverInfo.httpUrl}/api/metadata`);

      const items: CodeExplorerTreeItem[] = [];

      // 1. Connection Status
      const statusItem = new CodeExplorerTreeItem(
        'metadata-stat',
        'Connection Status',
        vscode.TreeItemCollapsibleState.None
      );
      statusItem.description = `Online (Port ${serverInfo.port})`;
      statusItem.iconPath = new vscode.ThemeIcon('pass');
      statusItem.tooltip = `Connected to CodeExplorer Server at ${serverInfo.httpUrl}`;
      items.push(statusItem);

      // 2. Total Nodes
      const totalNodesItem = new CodeExplorerTreeItem(
        'metadata-stat',
        'Total Nodes',
        vscode.TreeItemCollapsibleState.None
      );
      totalNodesItem.description = `${(meta.totalNodes || 0).toLocaleString()}`;
      totalNodesItem.iconPath = new vscode.ThemeIcon('symbol-structure');
      items.push(totalNodesItem);

      // 3. Total Relationships
      const totalEdgesItem = new CodeExplorerTreeItem(
        'metadata-stat',
        'Total Relationships',
        vscode.TreeItemCollapsibleState.None
      );
      totalEdgesItem.description = `${(meta.totalEdges || 0).toLocaleString()}`;
      totalEdgesItem.iconPath = new vscode.ThemeIcon('references');
      items.push(totalEdgesItem);

      // 4. Storage Engine
      const storageItem = new CodeExplorerTreeItem(
        'metadata-stat',
        'Storage Engine',
        vscode.TreeItemCollapsibleState.None
      );
      storageItem.description = 'SQLite WAL (.codeexplorer/graph.db)';
      storageItem.iconPath = new vscode.ThemeIcon('database');
      storageItem.tooltip = 'High-performance ACID graph store with Write-Ahead Logging';
      items.push(storageItem);

      return items;
    } catch (err: any) {
      const errItem = new CodeExplorerTreeItem('metadata-stat', `Error: ${err.message}`, vscode.TreeItemCollapsibleState.None);
      errItem.iconPath = new vscode.ThemeIcon('error');
      return [errItem];
    }
  }

  private getManagementItems(): CodeExplorerTreeItem[] {
    const serverInfo = this.processManager.getServerInfo();
    const isStarting = this.processManager.isStarting();

    const items: CodeExplorerTreeItem[] = [];

    // 1. Rescan Workspace (Incremental)
    const rescanItem = new CodeExplorerTreeItem(
      'management-item',
      'Rescan Workspace',
      vscode.TreeItemCollapsibleState.None
    );
    rescanItem.description = 'Incremental';
    rescanItem.iconPath = new vscode.ThemeIcon('sync');
    rescanItem.tooltip = 'Scan workspace for modified files and update graph incrementally';
    rescanItem.command = {
      command: 'codeExplorer.reindex',
      title: 'Rescan Workspace',
    };
    items.push(rescanItem);

    // 2. Rebuild Graph (Full Re-index)
    const rebuildItem = new CodeExplorerTreeItem(
      'management-item',
      'Rebuild Graph',
      vscode.TreeItemCollapsibleState.None
    );
    rebuildItem.description = 'Clear & re-index';
    rebuildItem.iconPath = new vscode.ThemeIcon('clear-all');
    rebuildItem.tooltip = 'Clear graph database and re-scan the entire workspace from scratch';
    rebuildItem.command = {
      command: 'codeExplorer.reindexFull',
      title: 'Rebuild Graph',
    };
    items.push(rebuildItem);

    // 3. Restart Server or Start Server
    if (serverInfo) {
      const restartItem = new CodeExplorerTreeItem(
        'management-item',
        'Restart Server',
        vscode.TreeItemCollapsibleState.None
      );
      restartItem.description = `Port ${serverInfo.port}`;
      restartItem.iconPath = new vscode.ThemeIcon('debug-restart');
      restartItem.tooltip = `Restart the CodeExplorer daemon (${serverInfo.httpUrl})`;
      restartItem.command = {
        command: 'codeExplorer.restartServer',
        title: 'Restart Server',
      };
      items.push(restartItem);
    } else {
      const startItem = new CodeExplorerTreeItem(
        'management-item',
        isStarting ? 'Starting Server...' : 'Start Server',
        vscode.TreeItemCollapsibleState.None
      );
      startItem.description = isStarting ? 'Launching daemon' : 'Offline';
      startItem.iconPath = new vscode.ThemeIcon(isStarting ? 'loading~spin' : 'play');
      startItem.tooltip = 'Start the CodeExplorer background server process';
      startItem.command = {
        command: 'codeExplorer.showGraph',
        title: 'Start Server',
      };
      items.push(startItem);
    }

    return items;
  }
}
