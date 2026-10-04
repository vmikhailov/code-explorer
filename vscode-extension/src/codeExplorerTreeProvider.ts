import * as vscode from 'vscode';
import * as http from 'http';
import * as path from 'path';
import * as fs from 'fs';
import { ProcessManager, ServerInfo } from './processManager';
import { getModelStatus } from './modelManager';

export interface MetadataDto {
  nodeCounts: Record<string, number>;
  relationshipCounts: Record<string, number>;
  layerCounts?: Record<number, number>;
  totalNodes: number;
  totalEdges: number;
  version?: string;
  lastUpdatedUtc?: string;
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
  isSystemNode?: boolean;
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
  serviceCount?: number;
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

export interface DomainDto {
  id: string;
  name: string;
  displayName: string;
  domainType: string;
  description?: string;
  icon?: string;
  boundedContextIds: string[];
  totalFiles: number;
  totalEntities: number;
}

export interface BoundedContextItemDto {
  id: string;
  name: string;
  displayName: string;
  domainId: string;
  domainName: string;
  domainType: string;
  summary?: string;
  fileCount: number;
  pureDomainCount: number;
  purityPercentage: number;
  targetEntities: string[];
  capabilities: string[];
  emittedEvents: string[];
  handledEvents: string[];
  projects: string[];
  bgColor: string;
  borderColor: string;
  size: number;
}

export interface BoundedContextMapDto {
  hasIntents: boolean;
  domains: DomainDto[];
  contexts: BoundedContextItemDto[];
  totalIntents: number;
  totalPureDomains: number;
}

export type TreeItemType =
  | 'root-domains'
  | 'root-diagrams'
  | 'root-layers'
  | 'root-management'
  | 'domain-item'
  | 'bounded-context-item'
  | 'context-section'
  | 'context-leaf-item'
  | 'diagram-item'
  | 'layer-group'
  | 'node-category'
  | 'rel-category'
  | 'services-container'
  | 'services-category'
  | 'ontology-service'
  | 'service-group'
  | 'service-item'
  | 'management-item'
  | 'management-status'
  | 'management-server'
  | 'management-graph'
  | 'management-rescan'
  | 'management-rebuild'
  | 'management-intent'
  | 'management-model'
  | 'management-model-download'
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
    this.contextValue = itemType;
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
  private _boundedContextsCache: BoundedContextMapDto | null = null;
  private _metadataCache: MetadataDto | null = null;

  refresh(): void {
    this._ontologyCache = null;
    this._servicesCache = null;
    this._serviceDetailsCache.clear();
    this._boundedContextsCache = null;
    this._metadataCache = null;
    this._onDidChangeTreeData.fire();
  }

  private async getMetadata(): Promise<MetadataDto | null> {
    if (this._metadataCache) {
      return this._metadataCache;
    }
    const serverInfo = await this.getServerInfo();
    if (serverInfo) {
      try {
        const meta = await fetchJson<MetadataDto>(`${serverInfo.httpUrl}/api/metadata`);
        if (meta) {
          this._metadataCache = meta;
          return meta;
        }
      } catch (e: any) {
        this.outputChannel.appendLine(`[TreeProvider] getMetadata error: ${e.message}`);
      }
    }
    return null;
  }

  private async getBoundedContextMap(): Promise<BoundedContextMapDto | null> {
    if (this._boundedContextsCache) {
      return this._boundedContextsCache;
    }
    const serverInfo = await this.getServerInfo();
    if (serverInfo) {
      try {
        const res = await fetchJson<BoundedContextMapDto>(`${serverInfo.httpUrl}/api/ontology/bounded-contexts`);
        if (res && res.contexts) {
          this._boundedContextsCache = res;
          return res;
        }
      } catch (e: any) {
        this.outputChannel.appendLine(`[TreeProvider] getBoundedContextMap error: ${e.message}`);
      }
    }
    return null;
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
      // 1. Domains & Bounded Contexts (Canonical DDD Architecture)
      const domainsRoot = new CodeExplorerTreeItem(
        'root-domains',
        'Domains & Bounded Contexts',
        vscode.TreeItemCollapsibleState.Expanded
      );
      domainsRoot.iconPath = new vscode.ThemeIcon('symbol-namespace');
      domainsRoot.tooltip = 'Domain-Driven Design Architecture: Problem Space (Domains) › Solution Space (Bounded Contexts) › Services › Capabilities';

      // 2. Architecture Diagrams
      const diagramsRoot = new CodeExplorerTreeItem(
        'root-diagrams',
        'Architecture Diagrams',
        vscode.TreeItemCollapsibleState.Expanded
      );
      diagramsRoot.iconPath = new vscode.ThemeIcon('layout');
      diagramsRoot.tooltip = 'Architecture diagrams, domain microservice maps, tiers, and project flow visualizers';

      // 3. Graph Layers (Ontology Layers 1 - 5)
      const layersRoot = new CodeExplorerTreeItem(
        'root-layers',
        'Graph Layers (Ontology 1 - 5)',
        vscode.TreeItemCollapsibleState.Expanded
      );
      layersRoot.iconPath = new vscode.ThemeIcon('layers');
      layersRoot.tooltip = 'Decoupled 5-layer ontology graph model';

      // 4. Management & System (Server, Database, Rescan, Intents, Lifecycle)
      const serverInfo = this.processManager.getServerInfo();
      const isStarting = this.processManager.isStarting();
      const metaCache = this._metadataCache;

      const managementRoot = new CodeExplorerTreeItem(
        'root-management',
        'Management',
        vscode.TreeItemCollapsibleState.Expanded
      );
      managementRoot.iconPath = new vscode.ThemeIcon('tools');
      managementRoot.description = serverInfo
        ? (metaCache ? `Online :${serverInfo.port} · ${metaCache.totalNodes.toLocaleString()} nodes` : `Online :${serverInfo.port}`)
        : isStarting ? 'Starting...' : 'Offline';
      managementRoot.tooltip = 'Graph server, SQLite WAL storage, AI models, and workspace lifecycle';

      return [diagramsRoot, layersRoot, domainsRoot, managementRoot];
    }

    if (element.itemType === 'root-domains') {
      return this.getDomainRootItems();
    }

    if (element.itemType === 'domain-item') {
      return this.getBoundedContextItemsForDomain(element.data?.domain, element.data?.map);
    }

    if (element.itemType === 'bounded-context-item') {
      return this.getBoundedContextDetailItems(element.data?.context);
    }

    if (element.itemType === 'context-section') {
      return this.getContextSectionLeafItems(element.data?.items);
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

    if (element.itemType === 'services-container' || element.itemType === 'services-category') {
      return this.getServicesListItems(element.data?.layerTitle, element.data?.kind);
    }

    if (element.itemType === 'ontology-service') {
      return this.getServiceOntologyGroups(element.data?.serviceName);
    }

    if (element.itemType === 'service-group') {
      return this.getServiceGroupItems(element.data?.serviceName, element.data?.categoryKey, element.data?.items);
    }


    if (element.itemType === 'root-management') {
      return this.getManagementItems();
    }

    return [];
  }

  private async getDomainRootItems(): Promise<CodeExplorerTreeItem[]> {
    const bcMap = await this.getBoundedContextMap();
    if (!bcMap || !bcMap.contexts || bcMap.contexts.length === 0) {
      const emptyItem = new CodeExplorerTreeItem(
        'empty-notice',
        'No Bounded Contexts Discovered',
        vscode.TreeItemCollapsibleState.None
      );
      emptyItem.description = 'Run ce scan or ce intent';
      emptyItem.iconPath = new vscode.ThemeIcon('info');
      return [emptyItem];
    }

    const items: CodeExplorerTreeItem[] = [];

    if (bcMap.domains && bcMap.domains.length > 0) {
      for (const dom of bcMap.domains) {
        const item = new CodeExplorerTreeItem(
          'domain-item',
          `${dom.icon || '🎯'} ${dom.displayName}`,
          vscode.TreeItemCollapsibleState.Expanded,
          { domain: dom, map: bcMap }
        );
        item.description = `${dom.boundedContextIds.length} ctx · ${dom.totalFiles} files`;
        item.tooltip = `${dom.displayName} Domain\n${dom.description || ''}\nBounded Contexts: ${dom.boundedContextIds.length}\nFiles: ${dom.totalFiles}\nEntities: ${dom.totalEntities}`;
        items.push(item);
      }
    } else {
      for (const ctx of bcMap.contexts) {
        items.push(this.createBoundedContextTreeItem(ctx));
      }
    }

    return items;
  }

  private getBoundedContextItemsForDomain(domain?: DomainDto, bcMap?: BoundedContextMapDto): CodeExplorerTreeItem[] {
    if (!domain || !bcMap || !bcMap.contexts) return [];
    const idSet = new Set(domain.boundedContextIds);
    const matching = bcMap.contexts.filter(c => idSet.has(c.id) || (c.domainId && c.domainId === domain.id));
    return matching.map(c => this.createBoundedContextTreeItem(c));
  }

  private createBoundedContextTreeItem(c: BoundedContextItemDto): CodeExplorerTreeItem {
    const item = new CodeExplorerTreeItem(
      'bounded-context-item',
      `📦 ${c.displayName}`,
      vscode.TreeItemCollapsibleState.Collapsed,
      { context: c }
    );
    const details: string[] = [];
    if (c.projects.length > 0) details.push(`${c.projects.length} proj`);
    details.push(`${c.fileCount} files`);
    if (c.purityPercentage > 0) details.push(`${c.purityPercentage}% pure`);
    item.description = details.join(' · ');
    item.tooltip = `Bounded Context: ${c.name}\nDomain: ${c.domainName}\n${c.summary || ''}\nEntities (${c.targetEntities.length}): ${c.targetEntities.join(', ')}`;
    return item;
  }

  private async getBoundedContextDetailItems(context?: BoundedContextItemDto): Promise<CodeExplorerTreeItem[]> {
    if (!context) return [];
    const items: CodeExplorerTreeItem[] = [];

    // Pre-cache services info if not already loaded
    const services = await this.getOntologyServices();
    const serviceMap = new Map<string, ServiceSummaryDto>();
    for (const s of services) {
      serviceMap.set(s.serviceName.toLowerCase(), s);
      serviceMap.set(s.serviceId.toLowerCase(), s);
    }

    if (context.projects && context.projects.length > 0) {
      for (const projName of context.projects) {
        const s = serviceMap.get(projName.toLowerCase());
        const item = new CodeExplorerTreeItem(
          'ontology-service',
          projName,
          vscode.TreeItemCollapsibleState.Collapsed,
          { serviceName: projName, serviceId: s?.serviceId || projName }
        );
        const icon = s?.kind === 'Worker' ? 'gear' : s?.kind === 'App' || s?.kind === 'FrontendApp' ? 'browser' : 'server-process';
        item.iconPath = new vscode.ThemeIcon(icon);

        if (s) {
          const parts: string[] = [];
          if (s.endpointCount > 0) parts.push(`${s.endpointCount} eps`);
          if (s.databaseCount > 0) parts.push(`${s.databaseCount} dbs`);
          if (s.topicCount > 0) parts.push(`${s.topicCount} topics`);
          if (s.serviceCount && s.serviceCount > 0) parts.push(`${s.serviceCount} svcs`);
          item.description = parts.length > 0 ? parts.join(' • ') : `Service (${s.kind})`;
        } else {
          item.description = 'Project';
        }

        item.tooltip = `Service/Project: ${projName}\nBounded Context: ${context.name}\nDomain: ${context.domainName}`;
        items.push(item);
      }
    }

    if (context.targetEntities && context.targetEntities.length > 0) {
      const entRoot = new CodeExplorerTreeItem(
        'context-section',
        `Entities (${context.targetEntities.length})`,
        vscode.TreeItemCollapsibleState.Collapsed,
        { items: context.targetEntities.map(e => ({ label: e, kind: 'entity' })) }
      );
      entRoot.iconPath = new vscode.ThemeIcon('symbol-class');
      entRoot.tooltip = `Ubiquitous Domain Entities: ${context.targetEntities.join(', ')}`;
      items.push(entRoot);
    }

    if ((!context.projects || context.projects.length === 0) && context.capabilities && context.capabilities.length > 0) {
      const capRoot = new CodeExplorerTreeItem(
        'context-section',
        `Capabilities (${context.capabilities.length})`,
        vscode.TreeItemCollapsibleState.Collapsed,
        { items: context.capabilities.map(cap => ({ label: cap, kind: 'capability' })) }
      );
      capRoot.iconPath = new vscode.ThemeIcon('zap');
      items.push(capRoot);
    }

    return items;
  }

  private getContextSectionLeafItems(items?: Array<{ label: string; kind: string }>): CodeExplorerTreeItem[] {
    if (!items || items.length === 0) return [];
    return items.map((it) => {
      const leaf = new CodeExplorerTreeItem(
        'context-leaf-item',
        it.label,
        vscode.TreeItemCollapsibleState.None
      );
      if (it.kind === 'entity') {
        leaf.iconPath = new vscode.ThemeIcon('symbol-class');
        leaf.tooltip = `Domain Entity / Aggregate Root: ${it.label}`;
      } else if (it.kind === 'project') {
        leaf.iconPath = new vscode.ThemeIcon('project');
        leaf.tooltip = `Project / Module: ${it.label}`;
      } else {
        leaf.iconPath = new vscode.ThemeIcon('symbol-method');
        leaf.tooltip = `Capability / Endpoint: ${it.label}`;
      }
      return leaf;
    });
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
      // {
      //   mode: 'c1',
      //   label: 'System Context',
      //   desc: 'Apps, Services & External APIs',
      //   icon: 'globe',
      //   tooltip: 'System Context — Apps, Services, and External Services',
      // },
      {
        mode: 'flow',
        label: 'Project Flow',
        desc: 'Focused dependency & call flow',
        icon: 'git-compare',
        tooltip: 'Project Flow — Topological dependency columns, call chains, and message flows',
      },
      // {
      //   mode: 'full',
      //   label: 'Project Dependency Graph',
      //   desc: 'Interactive full project graph',
      //   icon: 'type-hierarchy-sub',
      //   tooltip: 'Physical Dependency Graph — Complete workspace graph of all projects and dependencies',
      // },
      {
        mode: 'semantic',
        label: 'Domain Microservice Map',
        desc: 'Bounded contexts & clusters',
        icon: 'symbol-namespace',
        tooltip: 'Domain Architecture — Microservice domains and bounded contexts',
      },
      {
        mode: 'contexts',
        label: 'Bounded Context Map',
        desc: 'AI domain contexts & DDD map',
        icon: 'circuit-board',
        tooltip: 'Bounded Context Map — AI-distilled business domains, capabilities, CQRS roles, and ubiquitous entities',
      },
      // {
      //   mode: 'mermaid',
      //   label: 'Mermaid Architecture Diagram',
      //   desc: 'Mermaid flowchart renderer',
      //   icon: 'graph',
      //   tooltip: 'Mermaid Architecture Diagram — Interactive SVG diagram with exportable Markdown',
      // },
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
    if (!ontology || !ontology.layers || ontology.layers.length === 0) {
      return [];
    }

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
      return item;
    });
  }

  private async getLayerCategoryItems(layerId?: number, layerTitle?: string): Promise<CodeExplorerTreeItem[]> {
    if (!layerId) return [];

    const ontology = await this.getOntologyLayers();
    const layer = ontology?.layers?.find((l) => l.layerId === layerId);
    if (layer && layer.categories && layer.categories.length > 0) {
      return layer.categories
        .filter((cat) => !cat.isSystemNode && cat.kind !== 'DataSet')
        .map((cat) => {
        const isRel = cat.layerId === 5;
        const isServiceWorkload = cat.kind === 'Service' || cat.kind === 'App' || cat.kind === 'Worker' || cat.kind === 'CliTool';
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
        item.iconPath = new vscode.ThemeIcon(cat.icon || (isRel ? 'arrow-right' : 'symbol-misc'));
        if (!isServiceWorkload) {
          item.command = {
            command: 'codeExplorer.openNodeGrid',
            title: `Open ${cat.label} Grid`,
            arguments: [cat.kind, layerTitle || layer.title],
          };
          item.tooltip = isRel
            ? `${cat.count.toLocaleString()} ${cat.kind} relationships`
            : `${cat.count.toLocaleString()} ${cat.label}`;
        } else {
          item.tooltip = `${cat.count.toLocaleString()} ${cat.label}`;
        }
        return item;
      });
    }

    return [];
  }

  private async getServicesListItems(layerTitle?: string, kindFilter?: string): Promise<CodeExplorerTreeItem[]> {
    const services = await this.getOntologyServices();
    const targetKind = kindFilter || 'Service';
    const serviceList = services.filter((s) => s.kind === targetKind || (!s.kind && targetKind === 'Service'));
    return serviceList.map((s) => {
      const item = new CodeExplorerTreeItem(
        'ontology-service',
        s.serviceName,
        vscode.TreeItemCollapsibleState.Collapsed,
        { serviceName: s.serviceName, serviceId: s.serviceId, layerTitle }
      );
      const icon = s.kind === 'Worker' ? 'gear' : s.kind === 'App' || s.kind === 'FrontendApp' ? 'browser' : 'server-process';
      item.iconPath = new vscode.ThemeIcon(icon);
      const parts: string[] = [];
      if (s.endpointCount > 0) parts.push(`${s.endpointCount} eps`);
      if (s.databaseCount > 0) parts.push(`${s.databaseCount} dbs`);
      if (s.serviceCount && s.serviceCount > 0) parts.push(`${s.serviceCount} svcs`);
      if (s.externalCount > 0) parts.push(`${s.externalCount} ext`);
      item.description = parts.length > 0 ? parts.join(' • ') : `${s.endpointCount} eps • ${s.databaseCount} dbs`;

      const counts: string[] = [
        `Endpoints: ${s.endpointCount}`,
        `Databases: ${s.databaseCount}`,
        `Topics: ${s.topicCount}`,
      ];
      if (s.serviceCount !== undefined && s.serviceCount > 0) {
        counts.push(`Downstream Services: ${s.serviceCount}`);
      }
      counts.push(`External APIs: ${s.externalCount}`);
      item.tooltip = `${s.serviceName} (${s.kind}${s.framework ? ` • ${s.framework}` : ''}${s.language ? ` • ${s.language}` : ''})\n` +
        counts.join(' | ');
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
        : g.categoryKey === 'services' ? 'Service'
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
      return item;
    });
  }

  private getServiceGroupItems(serviceName?: string, categoryKey?: string, items?: ServiceCapabilityItemDto[]): CodeExplorerTreeItem[] {
    if (!items || items.length === 0) return [];

    const sorted = [...items].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));

    return sorted.map((it) => {
      const item = new CodeExplorerTreeItem(
        'service-item',
        it.name,
        vscode.TreeItemCollapsibleState.None,
        { serviceName, item: it }
      );

      const icon = it.kind === 'Endpoint' ? 'radio-tower'
        : it.kind === 'Database' ? 'database'
        : it.kind === 'Topic' ? 'mail'
        : (it.kind === 'Service' || it.kind === 'App' || it.kind === 'Worker' || it.kind === 'Project' || it.kind === 'FrontendApp') ? 'zap'
        : 'cloud';

      item.iconPath = new vscode.ThemeIcon(icon);
      item.description = it.details || (it.protocol ? `[${it.protocol}]` : '');
      if (it.filePath) {
        item.command = {
          command: 'codeExplorer.openSource',
          title: `Go to ${it.name}`,
          arguments: [it.filePath, it.line || 1],
        };
      }
      return item;
    });
  }

  private async getManagementItems(): Promise<CodeExplorerTreeItem[]> {
    const serverInfo = this.processManager.getServerInfo();
    const isStarting = this.processManager.isStarting();
    const meta = await this.getMetadata();
    const wsRoot = this.getWorkspaceRoot();

    // 1. Server status + version
    const engineVersion =
      meta?.version ||
      this.processManager.getBinaryManager()?.getCachedVersion() ||
      this.processManager.getBinaryManager()?.getExtensionVersion() ||
      '';
    const cleanVersion = engineVersion ? (engineVersion.startsWith('v') ? engineVersion : `v${engineVersion}`) : 'Unknown';

    const serverItem = new CodeExplorerTreeItem(
      'management-server',
      'Server',
      vscode.TreeItemCollapsibleState.None
    );
    if (serverInfo) {
      serverItem.description = `Online :${serverInfo.port} (${cleanVersion})`;
      serverItem.iconPath = new vscode.ThemeIcon('pass');
      serverItem.tooltip = `CodeExplorer daemon running at ${serverInfo.httpUrl}\nVersion: ${cleanVersion}\nUse inline buttons to restart server or check for engine updates.`;
    } else {
      serverItem.description = isStarting ? `Starting... (${cleanVersion})` : `Offline (${cleanVersion})`;
      serverItem.iconPath = new vscode.ThemeIcon(isStarting ? 'loading~spin' : 'circle-slash');
      serverItem.tooltip = isStarting ? 'Daemon is launching...' : `Daemon is offline. Engine version: ${cleanVersion}`;
    }

    // 2. Graph status + last update time
    let dbMtime: Date | null = null;
    if (wsRoot) {
      try {
        const dbPath = path.join(wsRoot, '.codeexplorer', 'graph.db');
        const walPath = path.join(wsRoot, '.codeexplorer', 'graph.db-wal');
        if (fs.existsSync(dbPath)) {
          dbMtime = fs.statSync(dbPath).mtime;
        }
        if (fs.existsSync(walPath)) {
          const walMtime = fs.statSync(walPath).mtime;
          if (!dbMtime || walMtime > dbMtime) {
            dbMtime = walMtime;
          }
        }
      } catch {}
    }
    if (!dbMtime && meta?.lastUpdatedUtc) {
      try {
        dbMtime = new Date(meta.lastUpdatedUtc);
      } catch {}
    }

    const updateStr = dbMtime ? this.formatGraphUpdateTime(dbMtime) : 'Not indexed yet';

    const graphItem = new CodeExplorerTreeItem(
      'management-graph',
      'Graph',
      vscode.TreeItemCollapsibleState.None
    );
    if (meta && meta.totalNodes > 0) {
      graphItem.description = `${meta.totalNodes.toLocaleString()} nodes · ${updateStr}`;
      graphItem.tooltip = `Knowledge Graph Storage: .codeexplorer/graph.db\nTotal Nodes: ${meta.totalNodes.toLocaleString()}\nTotal Relationships: ${meta.totalEdges.toLocaleString()}\nLast Updated: ${dbMtime ? dbMtime.toLocaleString() : 'Unknown'}\nUse inline buttons to scan workspace incrementally or run a full re-index.`;
    } else {
      graphItem.description = 'Not indexed yet';
      graphItem.tooltip = 'Knowledge graph has not been indexed yet. Use inline button to scan workspace.';
    }
    graphItem.iconPath = new vscode.ThemeIcon('database');

    // 3. Intent status + last update time
    const bcMap = await this.getBoundedContextMap();
    let intentMtime: Date | null = null;
    if (wsRoot) {
      try {
        const intentDbPath = path.join(wsRoot, '.codeexplorer', 'graph.db');
        if (fs.existsSync(intentDbPath)) {
          intentMtime = fs.statSync(intentDbPath).mtime;
        }
      } catch {}
    }

    const intentItem = new CodeExplorerTreeItem(
      'management-intent',
      'Intents',
      vscode.TreeItemCollapsibleState.None
    );
    if (bcMap && bcMap.hasIntents && bcMap.domains.length > 0) {
      const intentTimeStr = intentMtime ? this.formatGraphUpdateTime(intentMtime) : updateStr;
      intentItem.description = `${bcMap.domains.length} domains · ${intentTimeStr}`;
      intentItem.iconPath = new vscode.ThemeIcon('sparkle');
      intentItem.tooltip = `Architectural Intents & Bounded Contexts:\nDomains: ${bcMap.domains.length}\nBounded Contexts: ${bcMap.contexts.length}\nTotal Classified Intents: ${bcMap.totalIntents}\nLast Updated: ${intentMtime ? intentMtime.toLocaleString() : updateStr}\nUse inline buttons to distill intents or run full re-distill.`;
    } else {
      intentItem.description = 'Not distilled yet';
      intentItem.iconPath = new vscode.ThemeIcon('sparkle');
      intentItem.tooltip = 'Architectural intents have not been distilled yet. Use inline button to distill intents.';
    }

    return [serverItem, graphItem, intentItem];
  }

  private formatGraphUpdateTime(date: Date): string {
    const now = new Date();
    const diffMs = Math.max(0, now.getTime() - date.getTime());
    const diffMin = Math.floor(diffMs / 60000);

    const hours = date.getHours().toString().padStart(2, '0');
    const minutes = date.getMinutes().toString().padStart(2, '0');
    const timeStr = `${hours}:${minutes}`;

    if (diffMin < 1) {
      return `Updated just now (${timeStr})`;
    } else if (diffMin < 60) {
      return `Updated ${diffMin}m ago (${timeStr})`;
    } else if (now.toDateString() === date.toDateString()) {
      return `Updated today at ${timeStr}`;
    } else {
      const month = (date.getMonth() + 1).toString().padStart(2, '0');
      const day = date.getDate().toString().padStart(2, '0');
      return `Updated ${day}.${month} ${timeStr}`;
    }
  }
}
