import { ICommand } from './types';
import { GraphNode } from '../../../../proto/types';
import { EdgeCategory } from '../components/ProjectFlowView';

export type ViewMode = 'c1' | 'semantic' | 'layers' | 'flow' | 'full';

function getViewModeLabel(mode: ViewMode): string {
  switch (mode) {
    case 'c1':
      return 'C1: System Context & Boundaries';
    case 'layers':
      return 'System Layers';
    case 'flow':
      return 'Project Flow';
    case 'semantic':
      return 'Domain Microservice Map';
    case 'full':
      return 'Physical Graph';
    default:
      return mode;
  }
}

function getCategoryLabel(cat: EdgeCategory): string {
  switch (cat) {
    case 'service_call':
      return 'Service Calls';
    case 'database':
      return 'Database';
    case 'messaging':
      return 'Events/Queues';
    case 'library':
      return 'Libraries';
    default:
      return cat;
  }
}

/**
 * Command for changing view mode between semantic, layers, flow, and full graph.
 */
export class ChangeViewModeCommand implements ICommand {
  public readonly id = 'CHANGE_VIEW_MODE';

  constructor(
    private readonly prevMode: ViewMode,
    private readonly nextMode: ViewMode,
    private readonly prevProject: string,
    private readonly nextProject: string,
    private readonly setMode: (mode: ViewMode) => void,
    private readonly setProject: (project: string) => void,
    private readonly onRequestData?: (mode: ViewMode, project: string) => void
  ) {}

  public get description(): string {
    return `Switch to ${getViewModeLabel(this.nextMode)}`;
  }

  public execute(): void {
    this.setMode(this.nextMode);
    if (this.nextProject !== this.prevProject && this.nextProject) {
      this.setProject(this.nextProject);
    }
    this.onRequestData?.(this.nextMode, this.nextProject);
  }

  public undo(): void {
    this.setMode(this.prevMode);
    if (this.prevProject !== this.nextProject && this.prevProject) {
      this.setProject(this.prevProject);
    }
    this.onRequestData?.(this.prevMode, this.prevProject);
  }
}

/**
 * Command for selecting a project (and optionally switching to flow view).
 */
export class SelectProjectCommand implements ICommand {
  public readonly id = 'SELECT_PROJECT';

  constructor(
    private readonly prevProject: string,
    private readonly nextProject: string,
    private readonly prevMode: ViewMode,
    private readonly nextMode: ViewMode,
    private readonly setMode: (mode: ViewMode) => void,
    private readonly setProject: (project: string) => void,
    private readonly onRequestDependencies?: (project: string) => void
  ) {}

  public get description(): string {
    return `Select project: ${this.nextProject}`;
  }

  public execute(): void {
    if (this.nextMode !== this.prevMode) {
      this.setMode(this.nextMode);
    }
    this.setProject(this.nextProject);
    this.onRequestDependencies?.(this.nextProject);
  }

  public undo(): void {
    if (this.prevMode !== this.nextMode) {
      this.setMode(this.prevMode);
    }
    this.setProject(this.prevProject);
    if (this.prevProject) {
      this.onRequestDependencies?.(this.prevProject);
    }
  }
}

/**
 * Command for toggling test projects visibility.
 */
export class ToggleShowTestsCommand implements ICommand {
  public readonly id = 'TOGGLE_SHOW_TESTS';

  constructor(
    private readonly prevValue: boolean,
    private readonly nextValue: boolean,
    private readonly setShowTests: (val: boolean) => void
  ) {}

  public get description(): string {
    return this.nextValue ? 'Show test projects' : 'Hide test projects';
  }

  public execute(): void {
    this.setShowTests(this.nextValue);
  }

  public undo(): void {
    this.setShowTests(this.prevValue);
  }
}

/**
 * Command for toggling layer grouping in physical Cytoscape graph.
 */
export class ToggleGroupLayersCommand implements ICommand {
  public readonly id = 'TOGGLE_GROUP_LAYERS';

  constructor(
    private readonly prevValue: boolean,
    private readonly nextValue: boolean,
    private readonly setGroupLayers: (val: boolean) => void
  ) {}

  public get description(): string {
    return this.nextValue ? 'Group layers' : 'Ungroup layers';
  }

  public execute(): void {
    this.setGroupLayers(this.nextValue);
  }

  public undo(): void {
    this.setGroupLayers(this.prevValue);
  }
}

/**
 * Command for toggling an edge type in Project Flow view.
 */
export class ToggleFlowEdgeTypeCommand implements ICommand {
  public readonly id = 'TOGGLE_FLOW_EDGE_TYPE';

  constructor(
    private readonly category: EdgeCategory,
    private readonly prevTypes: Record<EdgeCategory, boolean>,
    private readonly nextTypes: Record<EdgeCategory, boolean>,
    private readonly setVisibleTypes: (types: Record<EdgeCategory, boolean>) => void
  ) {}

  public get description(): string {
    const isVisible = this.nextTypes[this.category];
    return `${isVisible ? 'Show' : 'Hide'} ${getCategoryLabel(this.category)}`;
  }

  public execute(): void {
    this.setVisibleTypes(this.nextTypes);
  }

  public undo(): void {
    this.setVisibleTypes(this.prevTypes);
  }
}

function formatCategoryLabel(category: string): string {
  switch (category) {
    case 'libsOut':
      return 'Libraries (uses)';
    case 'libsIn':
      return 'Libraries (used by)';
    case 'callsOut':
      return 'Service Calls (calls)';
    case 'acceptsIn':
      return 'Service Calls (serves)';
    case 'dbOut':
      return 'Database';
    case 'messagesOut':
      return 'Messages (sends)';
    case 'messagesIn':
      return 'Messages (receives)';
    default:
      return category;
  }
}

/**
 * Command for toggling a communication category on a card in Project Flow.
 */
export class ToggleFlowCategoryCommand implements ICommand {
  public readonly id = 'TOGGLE_FLOW_CATEGORY';

  constructor(
    private readonly projectName: string,
    private readonly category: string,
    private readonly isExpanding: boolean,
    private readonly prevCategories: Map<string, Set<string>>,
    private readonly nextCategories: Map<string, Set<string>>,
    private readonly prevCards: Set<string>,
    private readonly nextCards: Set<string>,
    private readonly setCategories: (cats: Map<string, Set<string>>) => void,
    private readonly setCards: (cards: Set<string>) => void
  ) {}

  public get description(): string {
    return `${this.isExpanding ? 'Expand' : 'Collapse'} ${formatCategoryLabel(this.category)} for ${this.projectName}`;
  }

  public execute(): void {
    this.setCategories(this.nextCategories);
    this.setCards(this.nextCards);
  }

  public undo(): void {
    this.setCategories(this.prevCategories);
    this.setCards(this.prevCards);
  }
}

/**
 * Command for expanding/collapsing a project card in Project Flow.
 */
export class ToggleFlowCardExpandCommand implements ICommand {
  public readonly id = 'TOGGLE_FLOW_CARD_EXPAND';

  constructor(
    private readonly projectName: string,
    private readonly isExpanding: boolean,
    private readonly prevCards: Set<string>,
    private readonly nextCards: Set<string>,
    private readonly setCards: (cards: Set<string>) => void
  ) {}

  public get description(): string {
    return `${this.isExpanding ? 'Expand' : 'Collapse'} card ${this.projectName}`;
  }

  public execute(): void {
    this.setCards(this.nextCards);
  }

  public undo(): void {
    this.setCards(this.prevCards);
  }
}

/**
 * Command for resetting Project Flow layout levels.
 */
export class ResetFlowLevelsCommand implements ICommand {
  public readonly id = 'RESET_FLOW_LEVELS';

  constructor(
    private readonly prevCategories: Map<string, Set<string>>,
    private readonly nextCategories: Map<string, Set<string>>,
    private readonly prevCards: Set<string>,
    private readonly nextCards: Set<string>,
    private readonly setCategories: (cats: Map<string, Set<string>>) => void,
    private readonly setCards: (cards: Set<string>) => void
  ) {}

  public get description(): string {
    return 'Reset Project Flow layout';
  }

  public execute(): void {
    this.setCategories(this.nextCategories);
    this.setCards(this.nextCards);
  }

  public undo(): void {
    this.setCategories(this.prevCategories);
    this.setCards(this.prevCards);
  }
}

/**
 * Command for collapsing/expanding an architectural layer.
 */
export class ToggleLayerCollapseCommand implements ICommand {
  public readonly id = 'TOGGLE_LAYER_COLLAPSE';

  constructor(
    private readonly layerId: string,
    private readonly layerName: string,
    private readonly isCollapsing: boolean,
    private readonly prevCollapsed: Set<string>,
    private readonly nextCollapsed: Set<string>,
    private readonly setCollapsed: (val: Set<string>) => void
  ) {}

  public get description(): string {
    return `${this.isCollapsing ? 'Collapse' : 'Expand'} layer: ${this.layerName}`;
  }

  public execute(): void {
    this.setCollapsed(this.nextCollapsed);
  }

  public undo(): void {
    this.setCollapsed(this.prevCollapsed);
  }
}

/**
 * Command for selecting or closing node details drawer in physical Cytoscape graph.
 */
export class SelectDrawerNodeCommand implements ICommand {
  public readonly id = 'SELECT_DRAWER_NODE';

  constructor(
    private readonly prevNode: GraphNode | null,
    private readonly nextNode: GraphNode | null,
    private readonly setSelectedNode: (node: GraphNode | null) => void
  ) {}

  public get description(): string {
    if (!this.nextNode) {
      return 'Close node inspector';
    }
    return `Inspect node: ${this.nextNode.displayName || this.nextNode.name}`;
  }

  public execute(): void {
    this.setSelectedNode(this.nextNode);
  }

  public undo(): void {
    this.setSelectedNode(this.prevNode);
  }
}
