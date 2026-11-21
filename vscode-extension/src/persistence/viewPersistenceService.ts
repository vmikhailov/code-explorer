import * as fs from 'fs';
import * as path from 'path';
import { DiagramId } from '../webview/core/diagramRegistry';
import { SessionPersistedState } from '../webview/core/graphSessionStore';

export interface ViewportTransform {
  zoom: number;
  panX: number;
  panY: number;
}

export interface PersistedDiagramView {
  schemaVersion: 1;
  diagramId: DiagramId;
  scopeKey: string;
  viewport?: ViewportTransform;
  session: SessionPersistedState;
  updatedAtUtc: string;
}

export interface KeyValueMemento {
  get<T>(key: string): T | undefined;
  update(key: string, value: unknown): Thenable<void> | Promise<void> | void;
}

/**
 * Two-tier view state persistence:
 * 1. Local auto-save in VS Code workspaceState (0 Git noise).
 * 2. Explicit team-shared view saved to .codeexplorer/views/<diagramId>.json.
 */
const SHARED_VIEWS_ROOT_DIR = '.codeexplorer';
const SHARED_VIEWS_SUBDIR = 'views';
const UTF8_ENCODING = 'utf8';

export class ViewPersistenceService {
  private readonly workspaceState: KeyValueMemento;
  private readonly workspaceRoot?: string;

  constructor(workspaceState: KeyValueMemento, workspaceRoot?: string) {
    this.workspaceState = workspaceState;
    this.workspaceRoot = workspaceRoot;
  }

  public static buildLocalKey(diagramId: DiagramId, scopeKey: string): string {
    const normScope = scopeKey.trim() || 'workspace';
    return `viewState:${diagramId}:${normScope}`;
  }

  public loadLocalView(diagramId: DiagramId, scopeKey: string): PersistedDiagramView | undefined {
    const key = ViewPersistenceService.buildLocalKey(diagramId, scopeKey);
    return this.workspaceState.get<PersistedDiagramView>(key);
  }

  public async saveLocalView(
    diagramId: DiagramId,
    scopeKey: string,
    session: SessionPersistedState,
    viewport?: ViewportTransform
  ): Promise<PersistedDiagramView> {
    const payload: PersistedDiagramView = {
      schemaVersion: 1,
      diagramId,
      scopeKey: scopeKey.trim() || 'workspace',
      viewport,
      session,
      updatedAtUtc: new Date().toISOString(),
    };
    const key = ViewPersistenceService.buildLocalKey(diagramId, scopeKey);
    await this.workspaceState.update(key, payload);
    return payload;
  }

  public getSharedViewPath(diagramId: DiagramId): string | undefined {
    if (!this.workspaceRoot) return undefined;
    return path.join(this.workspaceRoot, SHARED_VIEWS_ROOT_DIR, SHARED_VIEWS_SUBDIR, `${diagramId}.json`);
  }

  public loadSharedView(diagramId: DiagramId): PersistedDiagramView | undefined {
    const filePath = this.getSharedViewPath(diagramId);
    if (!filePath || !fs.existsSync(filePath)) return undefined;
    try {
      const raw = fs.readFileSync(filePath, UTF8_ENCODING);
      return JSON.parse(raw) as PersistedDiagramView;
    } catch {
      return undefined;
    }
  }

  public saveSharedView(
    diagramId: DiagramId,
    scopeKey: string,
    session: SessionPersistedState,
    viewport?: ViewportTransform
  ): string {
    const filePath = this.getSharedViewPath(diagramId);
    if (!filePath) {
      throw new Error('Cannot save shared view without a workspace root.');
    }
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    const payload: PersistedDiagramView = {
      schemaVersion: 1,
      diagramId,
      scopeKey: scopeKey.trim() || 'workspace',
      viewport,
      session,
      updatedAtUtc: new Date().toISOString(),
    };
    fs.writeFileSync(filePath, JSON.stringify(payload, null, 2), UTF8_ENCODING);
    return filePath;
  }
}
