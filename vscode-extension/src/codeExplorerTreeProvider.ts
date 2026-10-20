import * as vscode from 'vscode';
import { ProcessManager, ServerInfo } from './processManager';
import { ServerTransport } from './serverTransport';
import { GateSuiteSummaryDto, DatabaseEvidenceResponseDto } from '../../proto/types';
import { GateStateManager } from './gateStateManager';
import {
  MetadataDto,
  NodeDto,
  NodesResponseDto,
  OntologyCategoryDto,
  OntologyLayerDto,
  OntologyLayersResponseDto,
  ServiceSummaryDto,
  ServiceCapabilityItemDto,
  ServiceOntologyGroupDto,
  ServiceOntologyDetailsDto,
  DomainDto,
  BoundedContextItemDto,
  BoundedContextMapDto,
  TreeItemType,
  CodeExplorerTreeItem,
  buildGateRootItems,
  buildGateViolationItems,
  buildBoundedContextTreeItem,
  buildBoundedContextItemsForDomain,
  buildBoundedContextDetailItems,
  buildContextSectionLeafItems,
  buildDiagramItems,
  buildGraphLayerGroups,
  buildLayerCategoryItems,
  buildServicesListItems,
  buildServiceOntologyGroups,
  buildServiceGroupItems,
  buildDatabaseEvidenceItems,
  formatGraphUpdateTime,
  buildManagementItems,
} from './codeExplorerTreeHelpers';

// Re-export all types, interfaces, and helper items so existing imports from codeExplorerTreeProvider remain 100% compatible
export {
  MetadataDto,
  NodeDto,
  NodesResponseDto,
  OntologyCategoryDto,
  OntologyLayerDto,
  OntologyLayersResponseDto,
  ServiceSummaryDto,
  ServiceCapabilityItemDto,
  ServiceOntologyGroupDto,
  ServiceOntologyDetailsDto,
  DomainDto,
  BoundedContextItemDto,
  BoundedContextMapDto,
  TreeItemType,
  CodeExplorerTreeItem,
  buildGateRootItems,
  buildGateViolationItems,
  buildBoundedContextTreeItem,
  buildBoundedContextItemsForDomain,
  buildBoundedContextDetailItems,
  buildContextSectionLeafItems,
  buildDiagramItems,
  buildGraphLayerGroups,
  buildLayerCategoryItems,
  buildServicesListItems,
  buildServiceOntologyGroups,
  buildServiceGroupItems,
  buildDatabaseEvidenceItems,
  formatGraphUpdateTime,
  buildManagementItems,
};

export class CodeExplorerTreeDataProvider implements vscode.TreeDataProvider<CodeExplorerTreeItem> {
  private _onDidChangeTreeData: vscode.EventEmitter<CodeExplorerTreeItem | undefined | null | void> =
    new vscode.EventEmitter<CodeExplorerTreeItem | undefined | null | void>();
  readonly onDidChangeTreeData: vscode.Event<CodeExplorerTreeItem | undefined | null | void> =
    this._onDidChangeTreeData.event;

  constructor(
    private readonly processManager: ProcessManager,
    private readonly getWorkspaceRoot: () => string | undefined,
    private readonly outputChannel: vscode.OutputChannel,
    private readonly gateStateManager: GateStateManager = GateStateManager.getInstance(),
    private readonly transport: ServerTransport = ServerTransport.getInstance()
  ) {
    this.transport.onDidChangeState(() => {
      this.refresh();
    });
    this.gateStateManager.onDidChangeSuite(() => {
      this._onDidChangeTreeData.fire();
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

  setGatesSuite(suite: GateSuiteSummaryDto): void {
    this.gateStateManager.setSuite(suite);
  }

  async getGatesSuite(force = false): Promise<GateSuiteSummaryDto | null> {
    return this.gateStateManager.loadSuite(force);
  }

  private async getMetadata(): Promise<MetadataDto | null> {
    if (this._metadataCache) {
      return this._metadataCache;
    }
    const serverInfo = await this.getServerInfo();
    if (serverInfo) {
      try {
        const meta = await this.transport.get<MetadataDto>('/api/metadata', { autoStart: false });
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
        const res = await this.transport.get<BoundedContextMapDto>('/api/ontology/bounded-contexts', { autoStart: false });
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
        const res = await this.transport.get<OntologyLayersResponseDto>('/api/ontology/layers', { autoStart: false });
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
        const res = await this.transport.get<ServiceSummaryDto[]>('/api/ontology/services', { autoStart: false });
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
        const res = await this.transport.get<ServiceOntologyDetailsDto>(
          `/api/ontology/services/${encodeURIComponent(serviceName)}`,
          { autoStart: false }
        );
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
    if (!this.transport.hasWorkspace(root)) return null;

    return this.transport.getServerInfo();
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
      const domainsRoot = new CodeExplorerTreeItem(
        'root-domains',
        'Domains & Bounded Contexts',
        vscode.TreeItemCollapsibleState.Collapsed
      );
      domainsRoot.iconPath = new vscode.ThemeIcon('symbol-namespace');
      domainsRoot.tooltip = 'Domain-Driven Design Architecture: Problem Space (Domains) › Solution Space (Bounded Contexts) › Services › Capabilities';

      const diagramsRoot = new CodeExplorerTreeItem(
        'root-diagrams',
        'Architecture Diagrams',
        vscode.TreeItemCollapsibleState.Expanded
      );
      diagramsRoot.iconPath = new vscode.ThemeIcon('layout');
      diagramsRoot.tooltip = 'Architecture diagrams, domain microservice maps, tiers, and project flow visualizers';

      const layersRoot = new CodeExplorerTreeItem(
        'root-layers',
        'Graph Layers (Ontology 1 - 5)',
        vscode.TreeItemCollapsibleState.Collapsed
      );
      layersRoot.iconPath = new vscode.ThemeIcon('layers');
      layersRoot.tooltip = 'Decoupled 5-layer ontology graph model';

      const gatesCache = this.gateStateManager.getSuite();
      const isEvaluating = this.gateStateManager.isEvaluating();
      const gatesRoot = new CodeExplorerTreeItem(
        'root-gates',
        'Semantic Invariant Gates',
        vscode.TreeItemCollapsibleState.Collapsed
      );
      gatesRoot.id = 'root-gates';
      gatesRoot.iconPath = new vscode.ThemeIcon('shield');
      if (isEvaluating) {
        gatesRoot.description = 'Evaluating...';
      } else if (gatesCache) {
        const isEvaluated = gatesCache.gates && gatesCache.gates.length > 0 && gatesCache.gates.some((g) => g.status !== 'Pending');
        if (gatesCache.totalGates === 0) {
          gatesRoot.description = 'No gates configured';
        } else if (!isEvaluated) {
          gatesRoot.description = `${gatesCache.totalGates} configured · Not evaluated`;
        } else if (gatesCache.failedCount > 0) {
          gatesRoot.description = `${gatesCache.passedCount}/${gatesCache.totalGates} passed · ${gatesCache.newViolationsCount} new`;
        } else if (gatesCache.toleratedViolationsCount > 0) {
          gatesRoot.description = `${gatesCache.passedCount}/${gatesCache.totalGates} passed (${gatesCache.toleratedViolationsCount} tolerated)`;
        } else {
          gatesRoot.description = `${gatesCache.passedCount}/${gatesCache.totalGates} passed`;
        }
      } else {
        this.getServerInfo().then((info) => {
          if (info && !this.gateStateManager.isEvaluating() && !this.gateStateManager.getSuite()) {
            this.gateStateManager.loadSuite().catch(() => {});
          }
        });
      }
      gatesRoot.tooltip = 'Semantic Invariant Gates (SIG): Architectural fitness functions, OpenCypher checks, and ratchet baselines';

      const serverInfo = this.transport.getServerInfo();
      const isStarting = this.transport.isStarting();
      const metaCache = this._metadataCache;

      const managementRoot = new CodeExplorerTreeItem(
        'root-management',
        'Management',
        vscode.TreeItemCollapsibleState.Collapsed
      );
      managementRoot.iconPath = new vscode.ThemeIcon('tools');
      managementRoot.description = serverInfo
        ? (metaCache ? `Online :${serverInfo.port} · ${metaCache.totalNodes.toLocaleString()} nodes` : `Online :${serverInfo.port}`)
        : isStarting ? 'Starting...' : 'Offline';
      managementRoot.tooltip = 'Graph server, SQLite WAL storage, AI models, and workspace lifecycle';

      return [diagramsRoot, layersRoot, domainsRoot, gatesRoot, managementRoot];
    }

    if (element.itemType === 'root-gates') {
      const suite = await this.getGatesSuite();
      return buildGateRootItems(suite, this.gateStateManager.isEvaluating(), this.gateStateManager.evaluatingGateId());
    }

    if (element.itemType === 'gate-item') {
      return buildGateViolationItems(element.data?.gate);
    }

    if (element.itemType === 'root-domains') {
      return this.getDomainRootItems();
    }

    if (element.itemType === 'domain-item') {
      return buildBoundedContextItemsForDomain(element.data?.domain, element.data?.map);
    }

    if (element.itemType === 'bounded-context-item') {
      const services = await this.getOntologyServices();
      return buildBoundedContextDetailItems(element.data?.context, services);
    }

    if (element.itemType === 'context-section') {
      return buildContextSectionLeafItems(element.data?.items);
    }

    if (element.itemType === 'root-diagrams') {
      return buildDiagramItems();
    }

    if (element.itemType === 'root-layers') {
      const ontology = await this.getOntologyLayers();
      return buildGraphLayerGroups(ontology);
    }

    if (element.itemType === 'layer-group') {
      const ontology = await this.getOntologyLayers();
      return buildLayerCategoryItems(ontology, element.data?.layerId, element.data?.title);
    }

    if (element.itemType === 'services-container' || element.itemType === 'services-category') {
      const services = await this.getOntologyServices();
      return buildServicesListItems(services, element.data?.layerTitle, element.data?.kind);
    }

    if (element.itemType === 'database-category') {
      return this.getDatabaseListItems();
    }

    if (element.itemType === 'database-node-item') {
      return this.getDatabaseEvidenceItems(element.data?.databaseId, element.data?.serviceId);
    }

    if (element.itemType === 'ontology-service') {
      const details = await this.getServiceCapabilities(element.data?.serviceName);
      return buildServiceOntologyGroups(details, element.data?.serviceName);
    }

    if (element.itemType === 'service-group') {
      return buildServiceGroupItems(element.data?.serviceName, element.data?.categoryKey, element.data?.items);
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
          vscode.TreeItemCollapsibleState.Collapsed,
          { domain: dom, map: bcMap }
        );
        item.description = `${dom.boundedContextIds.length} ctx · ${dom.totalFiles} files`;
        item.tooltip = `${dom.displayName} Domain\n${dom.description || ''}\nBounded Contexts: ${dom.boundedContextIds.length}\nFiles: ${dom.totalFiles}\nEntities: ${dom.totalEntities}`;
        items.push(item);
      }
    } else {
      for (const ctx of bcMap.contexts) {
        items.push(buildBoundedContextTreeItem(ctx));
      }
    }

    return items;
  }

  private async getDatabaseListItems(): Promise<CodeExplorerTreeItem[]> {
    try {
      const resp = await this.transport.get<NodesResponseDto>('/api/nodes?kind=Database&limit=500');
      if (!resp || !resp.nodes || resp.nodes.length === 0) {
        return [new CodeExplorerTreeItem('empty-notice', 'No databases found', vscode.TreeItemCollapsibleState.None)];
      }

      const sorted = [...resp.nodes].sort((a, b) => (a.displayName || a.name).localeCompare(b.displayName || b.name));

      return sorted.map((n) => {
        const item = new CodeExplorerTreeItem(
          'database-node-item',
          n.displayName || n.name,
          vscode.TreeItemCollapsibleState.Collapsed,
          { databaseId: n.id, databaseName: n.name }
        );
        item.iconPath = new vscode.ThemeIcon('database');
        item.description = n.properties?.['engine'] || n.properties?.['db_type'] || 'database';
        item.tooltip = `${n.displayName || n.name}\nID: ${n.id}\nEngine: ${n.properties?.['engine'] || 'Unknown'}\nSchema: ${n.properties?.['schema'] || 'default'}`;
        return item;
      });
    } catch (err: any) {
      return [new CodeExplorerTreeItem('empty-notice', `Failed to load databases: ${err?.message || err}`, vscode.TreeItemCollapsibleState.None)];
    }
  }

  private async getDatabaseEvidenceItems(databaseId?: string, serviceId?: string): Promise<CodeExplorerTreeItem[]> {
    if (!databaseId) return [];
    try {
      const queryParams = new URLSearchParams({ id: databaseId });
      if (serviceId) queryParams.set('service', serviceId);
      const res = await this.transport.get<DatabaseEvidenceResponseDto>(`/api/database/evidence?${queryParams.toString()}`);
      return buildDatabaseEvidenceItems(res);
    } catch (err: any) {
      return [new CodeExplorerTreeItem('empty-notice', `Failed to load evidence: ${err?.message || err}`, vscode.TreeItemCollapsibleState.None)];
    }
  }

  private async getManagementItems(): Promise<CodeExplorerTreeItem[]> {
    const serverInfo = this.transport.getServerInfo();
    const isStarting = this.transport.isStarting();
    const meta = await this.getMetadata();
    const wsRoot = this.getWorkspaceRoot();

    let engineVersion = meta?.version;
    if (!engineVersion) {
      const best = this.processManager.getBinaryManager()?.getBestInstalledEngine(wsRoot);
      if (best) {
        engineVersion = best.version;
      } else {
        engineVersion =
          this.processManager.getBinaryManager()?.getCachedVersion() ||
          this.processManager.getBinaryManager()?.getExtensionVersion() ||
          '';
      }
    }

    const bcMap = await this.getBoundedContextMap();
    return buildManagementItems(serverInfo, isStarting, meta, wsRoot, engineVersion, bcMap);
  }
}
