import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { ServerInfo } from './processManager';
import {
  GateSuiteSummaryDto,
  GateItemDto,
  GateViolationDto,
  DatabaseEvidenceResponseDto,
  ArchitectureEvidenceItemDto,
} from '../../proto/types';

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
  primaryRole?: string;
  secondaryRoles?: string[];
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
  | 'database-category'
  | 'database-node-item'
  | 'database-evidence-item'
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
  | 'management-settings'
  | 'management-model'
  | 'management-model-download'
  | 'root-gates'
  | 'gate-item'
  | 'gate-violation-item'
  | 'gate-init-action'
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

export function buildGateRootItems(
  suite: GateSuiteSummaryDto | null,
  isGlobalEvaluating: boolean,
  evaluatingGateId: string | null
): CodeExplorerTreeItem[] {
  if (!suite || !suite.gates || suite.gates.length === 0) {
    const initGateItem = new CodeExplorerTreeItem(
      'gate-init-action',
      'Initialize Starter Gate (.yaml)',
      vscode.TreeItemCollapsibleState.None
    );
    initGateItem.description = '.codeexplorer/gates/';
    initGateItem.iconPath = new vscode.ThemeIcon('add');
    initGateItem.tooltip = 'Create a starter Semantic Invariant Gate YAML template in .codeexplorer/gates/';
    initGateItem.command = {
      command: 'codeExplorer.initGates',
      title: 'Initialize Starter Gate',
    };
    return [initGateItem];
  }

  return suite.gates.map((gate) => {
    const hasViolations = gate.violations && gate.violations.length > 0;
    const isGateEvaluating = isGlobalEvaluating && (!evaluatingGateId || evaluatingGateId === gate.id);
    const item = new CodeExplorerTreeItem(
      'gate-item',
      gate.name || gate.id,
      hasViolations && !isGateEvaluating ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.None,
      { gate }
    );
    item.id = `gate:${gate.id}`;

    if (!hasViolations) {
      item.command = {
        command: 'codeExplorer.openGateYaml',
        title: 'Edit Gate YAML Definition',
        arguments: [item],
      };
    }

    if (isGateEvaluating) {
      item.iconPath = new vscode.ThemeIcon('loading~spin', new vscode.ThemeColor('disabledForeground'));
      item.description = 'Evaluating...';
    } else if (gate.status === 'Pending') {
      item.iconPath = new vscode.ThemeIcon('circle-outline', new vscode.ThemeColor('disabledForeground'));
      item.description = 'Not evaluated';
    } else if (gate.status === 'FailedNew' || gate.status === 'Error') {
      item.iconPath = new vscode.ThemeIcon('error', new vscode.ThemeColor('testing.iconFailed'));
    } else if (gate.status === 'PassedTolerated') {
      item.iconPath = new vscode.ThemeIcon('warning', new vscode.ThemeColor('problemsWarningIcon.foreground'));
    } else {
      item.iconPath = new vscode.ThemeIcon('pass', new vscode.ThemeColor('testing.iconPassed'));
    }

    if (!isGateEvaluating && gate.status !== 'Pending') {
      const descParts: string[] = [];
      if (gate.newViolationsCount > 0) {
        descParts.push(`${gate.newViolationsCount} new`);
      }
      if (gate.toleratedViolationsCount > 0) {
        descParts.push(`${gate.toleratedViolationsCount} tolerated`);
      }
      if (descParts.length === 0) {
        descParts.push(gate.canRatchetDown ? 'Passed (can ratchet ↓)' : 'Passed');
      }
      item.description = descParts.join(' · ');
    }

    const tooltipLines = [
      `Gate: ${gate.name} (${gate.id})`,
      `Status: ${gate.status} (${gate.severity})`,
      `Category: ${gate.category} · Scope: ${gate.scope}`,
    ];
    if (gate.description) tooltipLines.push(gate.description);
    if (gate.remediation) tooltipLines.push(`Remediation: ${gate.remediation}`);
    if (gate.errorMessage) tooltipLines.push(`Error: ${gate.errorMessage}`);
    item.tooltip = tooltipLines.join('\n');

    return item;
  });
}

export function buildGateViolationItems(gate?: GateItemDto): CodeExplorerTreeItem[] {
  if (!gate || !gate.violations || gate.violations.length === 0) return [];

  return gate.violations.map((v: GateViolationDto) => {
    const label = v.symbol || (v.file ? path.basename(v.file) : v.target || 'Violation');
    const item = new CodeExplorerTreeItem(
      'gate-violation-item',
      label,
      vscode.TreeItemCollapsibleState.None,
      { violation: v, gateId: gate.id, filePath: v.file, line: v.line || 1 }
    );

    item.iconPath = new vscode.ThemeIcon(
      v.isTolerated ? 'info' : 'warning',
      new vscode.ThemeColor(v.isTolerated ? 'problemsInfoIcon.foreground' : 'testing.iconFailed')
    );

    const descParts: string[] = [];
    if (v.target) descParts.push(`→ ${v.target}`);
    if (v.file) descParts.push(`${path.basename(v.file)}${v.line > 0 ? `:${v.line}` : ''}`);
    if (v.isTolerated) descParts.push('[tolerated]');
    item.description = descParts.join(' · ');

    const tooltipLines = [
      v.isTolerated ? '[Tolerated Baseline Debt]' : '[New Gate Violation]',
      `Gate: ${gate.id}`,
    ];
    if (v.symbol) tooltipLines.push(`Symbol: ${v.symbol}`);
    if (v.target) tooltipLines.push(`Target: ${v.target}`);
    if (v.file) tooltipLines.push(`File: ${v.file}${v.line > 0 ? `:${v.line}` : ''}`);
    if (v.message) tooltipLines.push(`Message: ${v.message}`);
    item.tooltip = tooltipLines.join('\n');

    if (v.file) {
      item.command = {
        command: 'codeExplorer.openSource',
        title: 'Open Violation Source',
        arguments: [v.file, v.line || 1],
      };
    }

    return item;
  });
}

export function buildBoundedContextTreeItem(c: BoundedContextItemDto): CodeExplorerTreeItem {
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

export function buildBoundedContextItemsForDomain(
  domain?: DomainDto,
  bcMap?: BoundedContextMapDto
): CodeExplorerTreeItem[] {
  if (!domain || !bcMap || !bcMap.contexts) return [];
  const idSet = new Set(domain.boundedContextIds);
  const matching = bcMap.contexts.filter(c => idSet.has(c.id) || (c.domainId && c.domainId === domain.id));
  return matching.map(c => buildBoundedContextTreeItem(c));
}

export function buildBoundedContextDetailItems(
  context?: BoundedContextItemDto,
  services?: ServiceSummaryDto[]
): CodeExplorerTreeItem[] {
  if (!context) return [];
  const items: CodeExplorerTreeItem[] = [];

  const serviceMap = new Map<string, ServiceSummaryDto>();
  if (services) {
    for (const s of services) {
      serviceMap.set(s.serviceName.toLowerCase(), s);
      serviceMap.set(s.serviceId.toLowerCase(), s);
    }
  }

  if (context.projects && context.projects.length > 0) {
    for (const projName of context.projects) {
      const s = serviceMap.get(projName.toLowerCase());
      const secondarySuffix = s?.secondaryRoles && s.secondaryRoles.length > 0
        ? ` [+${s.secondaryRoles.map(r => r.slice(0, 3).toUpperCase()).join(', +')}]`
        : '';
      const item = new CodeExplorerTreeItem(
        'ontology-service',
        `${projName}${secondarySuffix}`,
        vscode.TreeItemCollapsibleState.Collapsed,
        { serviceName: projName, serviceId: s?.serviceId || projName }
      );
      const icon = s?.kind === 'Worker' ? 'gear' : s?.kind === 'App' || s?.kind === 'FrontendApp' ? 'browser' : s?.kind === 'TestSuite' ? 'beaker' : s?.kind === 'CliTool' ? 'terminal' : 'server-process';
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

export function buildContextSectionLeafItems(items?: Array<{ label: string; kind: string }>): CodeExplorerTreeItem[] {
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

export function buildDiagramItems(): CodeExplorerTreeItem[] {
  const diagrams = [
    {
      mode: 'contexts',
      label: 'Bounded Context Map',
      desc: 'AI domain contexts & DDD map',
      icon: 'circuit-board',
      tooltip: 'Bounded Context Map — AI-distilled business domains, capabilities, CQRS roles, and ubiquitous entities',
    },
    {
      mode: 'semantic',
      label: 'Architecture Map',
      desc: 'Services, resources & topology',
      icon: 'symbol-namespace',
      tooltip: 'Architecture Map — System services, resources, and topology',
    },
    {
      mode: 'project',
      label: 'Project Map',
      desc: 'Modules, calls & cycles inside a project',
      icon: 'type-hierarchy',
      tooltip: 'Project Map — Internal modules of one project, lifted call dependencies, resources, and dependency cycles',
    },
    {
      mode: 'layers',
      label: 'Architecture Tiers',
      desc: 'Tiered projects & databases',
      icon: 'layers',
      tooltip: 'Architecture Tiers — Tiered system view (Presentation, Application, Domain, Infrastructure)',
    },
    {
      mode: 'flow',
      label: 'Project Flow',
      desc: 'Focused dependency & call flow',
      icon: 'git-compare',
      tooltip: 'Project Flow — Topological dependency columns, call chains, and message flows',
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

export function buildGraphLayerGroups(ontology: OntologyLayersResponseDto | null): CodeExplorerTreeItem[] {
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
    item.description = `${l.totalCount.toLocaleString()} nodes`;
    item.iconPath = new vscode.ThemeIcon(l.icon || 'folder');
    item.tooltip = l.description;
    return item;
  });
}

export function buildLayerCategoryItems(
  ontology: OntologyLayersResponseDto | null,
  layerId?: number,
  layerTitle?: string
): CodeExplorerTreeItem[] {
  if (!layerId) return [];

  const layer = ontology?.layers?.find((l) => l.layerId === layerId);
  if (layer && layer.categories && layer.categories.length > 0) {
    return layer.categories
      .filter((cat) => !cat.isSystemNode && cat.kind !== 'DataSet')
      .map((cat) => {
      const isServiceWorkload = cat.kind === 'Service' || cat.kind === 'App' || cat.kind === 'Worker' || cat.kind === 'CliTool' || cat.kind === 'TestSuite';
      const isDatabase = cat.kind === 'Database';
      const itemType = isServiceWorkload ? 'services-category' : isDatabase ? 'database-category' : 'node-category';
      const collapsibleState = (isServiceWorkload || isDatabase) && cat.count > 0
        ? vscode.TreeItemCollapsibleState.Collapsed
        : vscode.TreeItemCollapsibleState.None;

      const item = new CodeExplorerTreeItem(
        itemType,
        cat.label,
        collapsibleState,
        {
          kind: cat.kind,
          layerTitle: layerTitle || layer.title,
        }
      );
      item.description = `${cat.count.toLocaleString()}`;
      const resolvedCatIcon = cat.icon === 'flask' ? 'beaker' : cat.icon;
      item.iconPath = new vscode.ThemeIcon(resolvedCatIcon || 'symbol-misc');
      if (!isServiceWorkload && !isDatabase) {
        item.command = {
          command: 'codeExplorer.openNodeGrid',
          title: `Open ${cat.label} Grid`,
          arguments: [cat.kind, layerTitle || layer.title],
        };
        item.tooltip = `${cat.count.toLocaleString()} ${cat.label}`;
      } else {
        item.tooltip = `${cat.count.toLocaleString()} ${cat.label}`;
      }
      return item;
    });
  }

  return [];
}

export function buildServicesListItems(
  services: ServiceSummaryDto[],
  layerTitle?: string,
  kindFilter?: string
): CodeExplorerTreeItem[] {
  const targetKind = kindFilter || 'Service';
  const serviceList = services.filter((s) => s.kind === targetKind || (!s.kind && targetKind === 'Service'));
  return serviceList.map((s) => {
    const secondarySuffix = s.secondaryRoles && s.secondaryRoles.length > 0
      ? ` [+${s.secondaryRoles.map(r => r.slice(0, 3).toUpperCase()).join(', +')}]`
      : '';
    const item = new CodeExplorerTreeItem(
      'ontology-service',
      `${s.serviceName}${secondarySuffix}`,
      vscode.TreeItemCollapsibleState.Collapsed,
      { serviceName: s.serviceName, serviceId: s.serviceId, layerTitle }
    );
    const icon = s.kind === 'Worker' ? 'gear' : s.kind === 'App' || s.kind === 'FrontendApp' ? 'browser' : s.kind === 'TestSuite' ? 'beaker' : s.kind === 'CliTool' ? 'terminal' : 'server-process';
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
    const secText = s.secondaryRoles && s.secondaryRoles.length > 0 ? ` [Secondary: ${s.secondaryRoles.join(', ')}]` : '';
    item.tooltip = `${s.serviceName} (${s.kind}${secText}${s.framework ? ` • ${s.framework}` : ''}${s.language ? ` • ${s.language}` : ''})\n` +
      counts.join(' | ');
    return item;
  });
}

export function buildServiceOntologyGroups(
  details: ServiceOntologyDetailsDto | null,
  serviceName?: string
): CodeExplorerTreeItem[] {
  if (!serviceName || !details || !details.groups) return [];

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

export function buildServiceGroupItems(
  serviceName?: string,
  categoryKey?: string,
  items?: ServiceCapabilityItemDto[]
): CodeExplorerTreeItem[] {
  if (!items || items.length === 0) return [];

  const sorted = [...items].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));

  return sorted.map((it) => {
    const isDb = it.kind === 'Database' || categoryKey === 'databases';
    const item = new CodeExplorerTreeItem(
      isDb ? 'database-node-item' : 'service-item',
      it.name,
      isDb ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.None,
      isDb ? { databaseId: it.id, databaseName: it.name, serviceId: serviceName } : { serviceName, item: it }
    );

    const icon = it.kind === 'Endpoint' ? 'radio-tower'
      : it.kind === 'Database' ? 'database'
      : it.kind === 'Topic' ? 'mail'
      : (it.kind === 'Service' || it.kind === 'App' || it.kind === 'Worker' || it.kind === 'Project' || it.kind === 'FrontendApp') ? 'zap'
      : 'cloud';

    item.iconPath = new vscode.ThemeIcon(icon);
    item.description = it.details || (it.protocol ? `[${it.protocol}]` : '');
    if (it.filePath && !isDb) {
      item.command = {
        command: 'codeExplorer.openSource',
        title: `Go to ${it.name}`,
        arguments: [it.filePath, it.line || 1],
      };
    }
    return item;
  });
}

export function buildDatabaseEvidenceItems(res: DatabaseEvidenceResponseDto | null): CodeExplorerTreeItem[] {
  if (!res || !res.items || res.items.length === 0) {
    return [new CodeExplorerTreeItem('empty-notice', 'No query or model evidence recorded', vscode.TreeItemCollapsibleState.None)];
  }

  return res.items.map((ev: ArchitectureEvidenceItemDto) => {
    const opTag = ev.operation ? `[${ev.operation.toUpperCase()}] ` : '';
    const item = new CodeExplorerTreeItem(
      'database-evidence-item',
      `${opTag}${ev.title}`,
      vscode.TreeItemCollapsibleState.None,
      { evidence: ev }
    );

    const filename = ev.filePath ? path.basename(ev.filePath) : '';
    const lineStr = ev.lineStart && ev.lineStart > 0 ? `:${ev.lineStart}` : '';
    item.description = ev.callerSymbol
      ? `${ev.callerSymbol} (${filename}${lineStr})`
      : filename ? `${filename}${lineStr}` : '';

    const tooltipParts: string[] = [];
    tooltipParts.push(`${ev.kind} (${ev.operation}) - ${ev.title}`);
    if (ev.callerSymbol) tooltipParts.push(`Caller: ${ev.callerSymbol}`);
    if (ev.filePath) tooltipParts.push(`File: ${ev.filePath}${lineStr}`);
    if (ev.reason) tooltipParts.push(`Reason: ${ev.reason}`);
    if (ev.codeSnippet) tooltipParts.push(`\n-- Snippet:\n${ev.codeSnippet}`);
    item.tooltip = tooltipParts.join('\n');

    const opUpper = (ev.operation || '').toUpperCase();
    const iconName = opUpper === 'SELECT' ? 'search'
      : opUpper === 'INSERT' ? 'diff-added'
      : opUpper === 'UPDATE' ? 'edit'
      : opUpper === 'DELETE' ? 'trash'
      : ev.kind === 'migration' ? 'history'
      : ev.kind === 'config' ? 'gear'
      : ev.kind === 'orm' || ev.kind === 'table' ? 'table'
      : 'database';

    item.iconPath = new vscode.ThemeIcon(iconName);

    if (ev.filePath) {
      item.command = {
        command: 'codeExplorer.openSource',
        title: `Go to ${ev.title}`,
        arguments: [ev.filePath, ev.lineStart && ev.lineStart > 0 ? ev.lineStart : undefined],
      };
    }

    return item;
  });
}

export function formatGraphUpdateTime(date: Date): string {
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

export function buildManagementItems(
  serverInfo: ServerInfo | null,
  isStarting: boolean,
  meta: MetadataDto | null,
  wsRoot: string | undefined,
  engineVersion: string | undefined,
  bcMap: BoundedContextMapDto | null
): CodeExplorerTreeItem[] {
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

  const updateStr = dbMtime ? formatGraphUpdateTime(dbMtime) : 'Not indexed yet';

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
    const intentTimeStr = intentMtime ? formatGraphUpdateTime(intentMtime) : updateStr;
    intentItem.description = `${bcMap.domains.length} domains · ${intentTimeStr}`;
    intentItem.iconPath = new vscode.ThemeIcon('sparkle');
    intentItem.tooltip = `Architectural Intents & Bounded Contexts:\nDomains: ${bcMap.domains.length}\nBounded Contexts: ${bcMap.contexts.length}\nTotal Classified Intents: ${bcMap.totalIntents}\nLast Updated: ${intentMtime ? intentMtime.toLocaleString() : updateStr}\nUse inline buttons to distill intents or run full re-distill.`;
  } else {
    intentItem.description = 'Not distilled yet';
    intentItem.iconPath = new vscode.ThemeIcon('sparkle');
    intentItem.tooltip = 'Architectural intents have not been distilled yet. Use inline button to distill intents.';
  }

  let excludeCount = 0;
  if (wsRoot) {
    try {
      const settingsPath = path.join(wsRoot, '.codeexplorer', 'settings.json');
      if (fs.existsSync(settingsPath)) {
        const raw = fs.readFileSync(settingsPath, 'utf-8');
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed?.exclude)) {
          excludeCount = parsed.exclude.filter((p: any) => typeof p === 'string' && p.trim().length > 0).length;
        }
      }
    } catch {}
  }

  const settingsItem = new CodeExplorerTreeItem(
    'management-settings',
    'Settings',
    vscode.TreeItemCollapsibleState.None
  );
  settingsItem.description =
    excludeCount > 0
      ? `${excludeCount} excluded ${excludeCount === 1 ? 'path' : 'paths'} · settings.json`
      : '.codeexplorer/settings.json';
  settingsItem.iconPath = new vscode.ThemeIcon('settings-gear');
  settingsItem.tooltip =
    `Workspace Scan Settings (.codeexplorer/settings.json)\n` +
    `Excluded paths/patterns: ${excludeCount}\n` +
    `Click to edit .codeexplorer/settings.json`;
  settingsItem.command = {
    command: 'codeExplorer.openWorkspaceSettings',
    title: 'Open Workspace Settings (.codeexplorer/settings.json)',
  };

  return [serverItem, graphItem, intentItem, settingsItem];
}
