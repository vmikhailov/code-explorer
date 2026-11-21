export type DiagramSection = 'system' | 'modules' | 'code' | 'data';

export type DiagramArchetype = 'map' | 'dependencies' | 'flow';

export type RendererAdapterKind = 'cytoscape' | 'reactflow' | 'swimlane';

export type DiagramId =
  | 'system-map'
  | 'service-flow'
  | 'project-dependencies'
  | 'layer-dependencies'
  | 'module-map'
  | 'class-map'
  | 'call-flow'
  | 'database-map'
  | 'data-flow';

export type DiagramPresetKey =
  | 'cyclesOnly'
  | 'sharedDatabasesOnly'
  | 'includeLibraries'
  | 'includeTests';

export type DiagramScopeMode = 'workspace' | 'service' | 'project' | 'entrypoint' | 'table';

export interface DiagramDefinition {
  readonly id: DiagramId;
  readonly section: DiagramSection;
  readonly title: string;
  readonly archetype: DiagramArchetype;
  readonly adapter: RendererAdapterKind;
  readonly serverViewType: string;
  readonly scopeMode: DiagramScopeMode;
  readonly supportedPresets: readonly DiagramPresetKey[];
  readonly defaultPresets: Partial<Record<DiagramPresetKey, boolean>>;
  readonly supportsGateOverlay: boolean;
  readonly description: string;
}

export const DIAGRAM_DEFINITIONS: readonly DiagramDefinition[] = [
  {
    id: 'system-map',
    section: 'system',
    title: 'System Map',
    archetype: 'map',
    adapter: 'cytoscape',
    serverViewType: 'system-context',
    scopeMode: 'workspace',
    supportedPresets: ['includeLibraries', 'includeTests', 'sharedDatabasesOnly'],
    defaultPresets: { includeLibraries: false, includeTests: false },
    supportsGateOverlay: true,
    description: 'Macro system contour: services, workers, apps, external APIs, brokers, and databases.',
  },
  {
    id: 'service-flow',
    section: 'system',
    title: 'Service Flow',
    archetype: 'flow',
    adapter: 'reactflow',
    serverViewType: 'service-flow',
    scopeMode: 'service',
    supportedPresets: ['includeLibraries'],
    defaultPresets: { includeLibraries: false },
    supportsGateOverlay: true,
    description: 'Runtime inter-service communication over HTTP, gRPC, and messaging topics.',
  },
  {
    id: 'project-dependencies',
    section: 'modules',
    title: 'Project Dependencies',
    archetype: 'dependencies',
    adapter: 'cytoscape',
    serverViewType: 'project-dependencies',
    scopeMode: 'project',
    supportedPresets: ['cyclesOnly', 'includeLibraries', 'includeTests'],
    defaultPresets: { cyclesOnly: false, includeLibraries: true, includeTests: false },
    supportsGateOverlay: true,
    description: 'Project and package dependency graph with Tarjan SCC circular dependency detection.',
  },
  {
    id: 'layer-dependencies',
    section: 'modules',
    title: 'Layer Dependencies',
    archetype: 'dependencies',
    adapter: 'swimlane',
    serverViewType: 'layer-dependencies',
    scopeMode: 'workspace',
    supportedPresets: ['includeLibraries', 'includeTests'],
    defaultPresets: { includeLibraries: true, includeTests: false },
    supportsGateOverlay: true,
    description: 'Layered architecture tiers (Ingress -> Application -> Domain -> Infrastructure -> Tests).',
  },
  {
    id: 'module-map',
    section: 'modules',
    title: 'Module Map',
    archetype: 'map',
    adapter: 'cytoscape',
    serverViewType: 'module-map',
    scopeMode: 'project',
    supportedPresets: ['cyclesOnly', 'includeTests'],
    defaultPresets: { cyclesOnly: false, includeTests: false },
    supportsGateOverlay: true,
    description: 'Internal folders and modules of a single project with lifted calls and cycle detection.',
  },
  {
    id: 'class-map',
    section: 'code',
    title: 'Class Map',
    archetype: 'map',
    adapter: 'cytoscape',
    serverViewType: 'class-map',
    scopeMode: 'project',
    supportedPresets: ['includeTests'],
    defaultPresets: { includeTests: false },
    supportsGateOverlay: true,
    description: 'Classes, interfaces, inheritance, and DI dependencies inside a project or namespace.',
  },
  {
    id: 'call-flow',
    section: 'code',
    title: 'Call Flow',
    archetype: 'flow',
    adapter: 'reactflow',
    serverViewType: 'call-flow',
    scopeMode: 'entrypoint',
    supportedPresets: [],
    defaultPresets: {},
    supportsGateOverlay: true,
    description: 'Step-by-step directed call trace from entry point / endpoint down to DB and egress.',
  },
  {
    id: 'database-map',
    section: 'data',
    title: 'Database Map',
    archetype: 'map',
    adapter: 'cytoscape',
    serverViewType: 'database-map',
    scopeMode: 'workspace',
    supportedPresets: ['sharedDatabasesOnly'],
    defaultPresets: { sharedDatabasesOnly: false },
    supportsGateOverlay: true,
    description: 'Services and projects mapped to databases, schemas, and topics with shared-DB highlighting.',
  },
  {
    id: 'data-flow',
    section: 'data',
    title: 'Data Flow',
    archetype: 'flow',
    adapter: 'reactflow',
    serverViewType: 'data-flow',
    scopeMode: 'table',
    supportedPresets: [],
    defaultPresets: {},
    supportsGateOverlay: true,
    description: 'Writers -> Table / View / Topic -> Readers lineage projection.',
  },
] as const;

const REGISTRY_BY_ID = new Map<DiagramId, DiagramDefinition>(
  DIAGRAM_DEFINITIONS.map((def) => [def.id, def])
);

export function getDiagramDefinition(id: DiagramId): DiagramDefinition {
  const found = REGISTRY_BY_ID.get(id);
  if (!found) {
    throw new Error(`Unknown diagram definition id: ${id}`);
  }
  return found;
}

export function getDiagramsBySection(section: DiagramSection): DiagramDefinition[] {
  return DIAGRAM_DEFINITIONS.filter((d) => d.section === section);
}
