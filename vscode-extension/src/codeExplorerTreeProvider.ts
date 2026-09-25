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

export interface OntologyCategoryDto {
  kind: string;
  label: string;
  icon: string;
  count: number;
  layerId: number;
}

export interface OntologyLayerDto {
  layerId: number;
  name: string;
  title: string;
  description: string;
  icon: string;
  totalCount: number;
  categories: OntologyCategoryDto[];
}

export interface OntologyLayersResponseDto {
  layers: OntologyLayerDto[];
  totalNodes: number;
  totalEdges: number;
}

export interface ServiceSummaryDto {
  serviceName: string;
  serviceId: string;
  kind: string;
  framework?: string;
  language?: string;
  endpointCount: number;
  databaseCount: number;
  topicCount: number;
  externalCount: number;
}

export interface ServiceCapabilityItemDto {
  id: string;
  name: string;
  kind: string;
  protocol?: string;
  method?: string;
  route?: string;
  filePath?: string;
  line?: number;
  details?: string;
}

export interface ServiceOntologyGroupDto {
  categoryKey: string;
  label: string;
  icon: string;
  count: number;
  items: ServiceCapabilityItemDto[];
}

export interface ServiceOntologyDetailsDto {
  serviceName: string;
  serviceId: string;
  kind: string;
  framework?: string;
  language?: string;
  groups: ServiceOntologyGroupDto[];
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
  | 'services-container'
  | 'services-category'
  | 'ontology-service'
  | 'service-group'
  | 'service-item'
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

  private _ontologyCache: OntologyLayersResponseDto | null = null;
  private _servicesCache: ServiceSummaryDto[] | null = null;
  private _serviceDetailsCache = new Map<string, ServiceOntologyDetailsDto>();

  refresh(): void {
    this._ontologyCache = null;
    this._servicesCache = null;
    this._serviceDetailsCache.clear();
    this._onDidChangeTreeData.fire();
  }

  private async getOntologyLayers(): Promise<OntologyLayersResponseDto | null> {
    if (this._ontologyCache) {
      return this._ontologyCache;
    }
    const serverInfo = await this.getServerInfo();
    if (serverInfo) {
      try {
        const res = await fetchJson<OntologyLayersResponseDto>(`${serverInfo.httpUrl}/api/ontology/layers`);
        if (res && res.layers && res.layers.length > 0) {
          this._ontologyCache = res;
          return res;
        }
      } catch {}
    }
    return null;
  }

  private async getOntologyServices(): Promise<ServiceSummaryDto[]> {
    if (this._servicesCache && this._servicesCache.length > 0) {
      return this._servicesCache;
    }
    const serverInfo = await this.getServerInfo();
    if (serverInfo) {
      try {
        const res = await fetchJson<ServiceSummaryDto[]>(`${serverInfo.httpUrl}/api/ontology/services`);
        if (Array.isArray(res)) {
          this._servicesCache = res;
          return res;
        }
      } catch (e: any) {
        this.outputChannel.appendLine(`[TreeProvider] getOntologyServices error: ${e.message}`);
      }
    }
    return [];
  }

  private async getServiceCapabilities(serviceName: string): Promise<ServiceOntologyDetailsDto | null> {
    if (this._serviceDetailsCache.has(serviceName)) {
      return this._serviceDetailsCache.get(serviceName)!;
    }
    const serverInfo = await this.getServerInfo();
    if (serverInfo) {
      try {
        const res = await fetchJson<ServiceOntologyDetailsDto>(`${serverInfo.httpUrl}/api/ontology/services/${encodeURIComponent(serviceName)}`);
        if (res && res.groups) {
          this._serviceDetailsCache.set(serviceName, res);
          return res;
        }
      } catch (e: any) {
        this.outputChannel.appendLine(`[TreeProvider] getServiceCapabilities error: ${e.message}`);
      }
    }
    return null;
  }

  private async getServerInfo(): Promise<ServerInfo | null> {
    const root = this.getWorkspaceRoot();
    if (!root) return null;
    if (!this.processManager.hasWorkspace(root)) return null;

    return this.processManager.getServerInfo();
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
      layersRoot.command = {
        command: 'codeExplorer.openNodeGrid',
        title: 'Browse All Graph Layers in Grid',
        arguments: ['all', 'Graph Layers (Ontology 1 - 5)'],
      };

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

    if (element.itemType === 'services-container') {
      return this.getServicesListItems(element.data?.layerTitle);
    }

    if (element.itemType === 'ontology-service') {
      return this.getServiceOntologyGroups(element.data?.serviceName);
    }

    if (element.itemType === 'service-group') {
      return this.getServiceGroupItems(element.data?.serviceName, element.data?.categoryKey, element.data?.items);
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
        label: 'Architecture Tiers',
        desc: 'Tiered projects & databases',
        icon: 'layers',
        tooltip: 'Architecture Tiers — Tiered system view (Presentation, Application, Domain, Infrastructure)',
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
    const ontology = await this.getOntologyLayers();
    if (ontology && ontology.layers && ontology.layers.length > 0) {
      return ontology.layers.map((l) => {
        const item = new CodeExplorerTreeItem(
          'layer-group',
          l.title || `Layer ${l.layerId}: ${l.name}`,
          vscode.TreeItemCollapsibleState.Collapsed,
          { layerId: l.layerId, title: l.title || l.name }
        );
        item.description = `${l.totalCount.toLocaleString()} ${l.layerId === 5 ? 'edges' : 'nodes'}`;
        item.iconPath = new vscode.ThemeIcon(l.icon || (l.layerId === 5 ? 'references' : 'folder'));
        item.tooltip = l.description;
        item.command = {
          command: 'codeExplorer.openNodeGrid',
          title: `Browse ${l.title || l.name} in Grid`,
          arguments: [
            l.layerId === 5 ? 'Layer5_Relationships' : `Layer${l.layerId}`,
            l.title || `Layer ${l.layerId}: ${l.name}`,
          ],
        };
        return item;
      });
    }

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
      (counts['Service'] || 0) +
      (counts['App'] || 0) +
      (counts['FrontendApp'] || 0) +
      (counts['Worker'] || 0) +
      (counts['Library'] || 0) +
      (counts['SharedLibrary'] || 0) +
      (counts['CliTool'] || 0) +
      (counts['EntryPoint'] || 0) +
      (counts['Endpoint'] || 0) +
      (counts['Procedure'] || 0) +
      (counts['Database'] || 0) +
      (counts['Table'] || 0) +
      (counts['DataSet'] || 0) +
      (counts['Topic'] || 0) +
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
      item.command = {
        command: 'codeExplorer.openNodeGrid',
        title: `Browse ${l.title} in Grid`,
        arguments: [
          l.layerId === 5 ? 'Layer5_Relationships' : `Layer${l.layerId}`,
          l.title,
        ],
      };
      return item;
    });
  }

  private async getLayerCategoryItems(layerId?: number, layerTitle?: string): Promise<CodeExplorerTreeItem[]> {
    if (!layerId) return [];

    const ontology = await this.getOntologyLayers();
    const layer = ontology?.layers?.find((l) => l.layerId === layerId);
    if (layer && layer.categories && layer.categories.length > 0) {
      return layer.categories.map((cat) => {
        const isRel = cat.layerId === 5;
        const isServiceWorkload = cat.kind === 'Service';
        const itemType = isRel ? 'rel-category' : isServiceWorkload ? 'services-category' : 'node-category';
        const collapsibleState = isServiceWorkload && cat.count > 0
          ? vscode.TreeItemCollapsibleState.Collapsed
          : vscode.TreeItemCollapsibleState.None;

        const item = new CodeExplorerTreeItem(
          itemType,
          cat.label,
          collapsibleState,
          {
            kind: cat.kind,
            rel: isRel ? cat.kind : undefined,
            layerTitle: layerTitle || layer.title,
          }
        );
        item.description = `${cat.count.toLocaleString()}`;
        item.iconPath = new vscode.ThemeIcon(cat.icon || (isRel ? 'arrow-right' : 'symbol-class'));
        item.tooltip = isRel
          ? `${cat.count.toLocaleString()} ${cat.kind} relationships`
          : `Click to browse all ${cat.count.toLocaleString()} ${cat.label} in central grid`;
        item.command = {
          command: 'codeExplorer.openNodeGrid',
          title: `Browse ${cat.label} in Grid`,
          arguments: [cat.kind, layerTitle || layer.title],
        };
        return item;
      });
    }

    return [];
  }

  private async getServicesListItems(layerTitle?: string): Promise<CodeExplorerTreeItem[]> {
    const services = await this.getOntologyServices();
    return services.map((s) => {
      const item = new CodeExplorerTreeItem(
        'ontology-service',
        s.serviceName,
        vscode.TreeItemCollapsibleState.Collapsed,
        { serviceName: s.serviceName, serviceId: s.serviceId, layerTitle }
      );
      const icon = s.kind === 'Worker' ? 'gear' : s.kind === 'App' || s.kind === 'FrontendApp' ? 'browser' : 'server-process';
      item.iconPath = new vscode.ThemeIcon(icon);
      item.description = `${s.endpointCount} eps • ${s.databaseCount} dbs`;
      item.tooltip = `${s.serviceName} (${s.kind}${s.framework ? ` • ${s.framework}` : ''}${s.language ? ` • ${s.language}` : ''})\n` +
        `Endpoints: ${s.endpointCount} | Databases: ${s.databaseCount} | Topics: ${s.topicCount} | External APIs: ${s.externalCount}`;
      item.command = {
        command: 'codeExplorer.openNodeGrid',
        title: `Browse ${s.serviceName} in Grid`,
        arguments: ['all', `Layer 4 › ${s.serviceName}`, s.serviceName],
      };
      return item;
    });
  }

  private async getServiceOntologyGroups(serviceName?: string): Promise<CodeExplorerTreeItem[]> {
    if (!serviceName) return [];
    const details = await this.getServiceCapabilities(serviceName);
    if (!details || !details.groups) return [];

    return details.groups.map((g) => {
      const kind = g.categoryKey === 'endpoints' ? 'Endpoint'
        : g.categoryKey === 'databases' ? 'Database'
        : g.categoryKey === 'topics' ? 'Topic'
        : 'ExternalService';

      const item = new CodeExplorerTreeItem(
        'service-group',
        `${g.label} (${g.count})`,
        g.count > 0 ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.None,
        { serviceName, categoryKey: g.categoryKey, items: g.items, kind }
      );
      item.iconPath = new vscode.ThemeIcon(g.icon || 'folder');
      item.description = `${g.count}`;
      item.tooltip = `${g.label} (${g.count}) belonging to ${serviceName} in graph ontology`;
      item.command = {
        command: 'codeExplorer.openNodeGrid',
        title: `Browse ${serviceName} ${g.label} in Grid`,
        arguments: [kind, `${serviceName} › ${g.label}`, serviceName],
      };
      return item;
    });
  }

  private getServiceGroupItems(serviceName?: string, categoryKey?: string, items?: ServiceCapabilityItemDto[]): CodeExplorerTreeItem[] {
    if (!items || items.length === 0) return [];

    return items.map((it) => {
      const item = new CodeExplorerTreeItem(
        'service-item',
        it.name,
        vscode.TreeItemCollapsibleState.None,
        { serviceName, item: it }
      );

      const icon = it.kind === 'Endpoint' ? 'radio-tower'
        : it.kind === 'Database' ? 'database'
        : it.kind === 'Topic' ? 'mail'
        : 'cloud';

      item.iconPath = new vscode.ThemeIcon(icon);
      item.description = it.details || (it.protocol ? `[${it.protocol}]` : '');
      item.tooltip = `${it.name}\nType: ${it.kind}\n${it.filePath ? `Location: ${it.filePath}${it.line ? `:${it.line}` : ''}` : ''}`;
      item.command = {
        command: 'codeExplorer.openNodeGrid',
        title: `Browse ${it.name} in Grid`,
        arguments: [it.kind, `${serviceName} › ${it.name}`, serviceName],
      };
      return item;
    });
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
