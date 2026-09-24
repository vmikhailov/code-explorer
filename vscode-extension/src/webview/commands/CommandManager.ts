import { ICommand, ICommandManager, CommandManagerSnapshot } from './types';

export class CommandManager implements ICommandManager {
  private scopes: Map<string, { undoStack: ICommand[]; redoStack: ICommand[] }> = new Map();
  private currentScope: string = 'default';
  private readonly maxHistorySize: number;
  private listeners: Set<() => void> = new Set();
  private version: number = 0;
  private cachedSnapshot: CommandManagerSnapshot | null = null;

  constructor(maxHistorySize: number = 100) {
    this.maxHistorySize = maxHistorySize;
    this.scopes.set('default', { undoStack: [], redoStack: [] });
  }

  public setScope(scope: string): void {
    if (!scope || this.currentScope === scope) return;
    this.currentScope = scope;
    if (!this.scopes.has(scope)) {
      this.scopes.set(scope, { undoStack: [], redoStack: [] });
    }
    this.notifyChange();
  }

  public getScope(): string {
    return this.currentScope;
  }

  private getActiveStacks(): { undoStack: ICommand[]; redoStack: ICommand[] } {
    let s = this.scopes.get(this.currentScope);
    if (!s) {
      s = { undoStack: [], redoStack: [] };
      this.scopes.set(this.currentScope, s);
    }
    return s;
  }

  public executeCommand(command: ICommand): void {
    try {
      command.execute();
    } catch (err) {
      console.error(`[CommandManager] Error executing command '${command.description}':`, err);
      throw err;
    }

    const { undoStack, redoStack } = this.getActiveStacks();
    undoStack.push(command);
    if (undoStack.length > this.maxHistorySize) {
      undoStack.shift();
    }

    // A new action clears the redo stack for the current scope
    redoStack.length = 0;
    this.notifyChange();
  }

  public undo(): boolean {
    const { undoStack, redoStack } = this.getActiveStacks();
    if (undoStack.length === 0) {
      return false;
    }

    const command = undoStack.pop()!;
    try {
      command.undo();
    } catch (err) {
      console.error(`[CommandManager] Error undoing command '${command.description}':`, err);
      // Restore back to undo stack so state isn't permanently corrupted
      undoStack.push(command);
      this.notifyChange();
      throw err;
    }

    redoStack.push(command);
    this.notifyChange();
    return true;
  }

  public redo(): boolean {
    const { undoStack, redoStack } = this.getActiveStacks();
    if (redoStack.length === 0) {
      return false;
    }

    const command = redoStack.pop()!;
    try {
      command.execute();
    } catch (err) {
      console.error(`[CommandManager] Error redoing command '${command.description}':`, err);
      redoStack.push(command);
      this.notifyChange();
      throw err;
    }

    undoStack.push(command);
    this.notifyChange();
    return true;
  }

  public get canUndo(): boolean {
    const { undoStack } = this.getActiveStacks();
    return undoStack.length > 0;
  }

  public get canRedo(): boolean {
    const { redoStack } = this.getActiveStacks();
    return redoStack.length > 0;
  }

  public get undoDescription(): string | null {
    const { undoStack } = this.getActiveStacks();
    return undoStack.length > 0 ? undoStack[undoStack.length - 1].description : null;
  }

  public get redoDescription(): string | null {
    const { redoStack } = this.getActiveStacks();
    return redoStack.length > 0 ? redoStack[redoStack.length - 1].description : null;
  }

  public clear(): void {
    const { undoStack, redoStack } = this.getActiveStacks();
    undoStack.length = 0;
    redoStack.length = 0;
    this.notifyChange();
  }

  public subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  public getSnapshot(): CommandManagerSnapshot {
    if (this.cachedSnapshot && this.cachedSnapshot.version === this.version) {
      return this.cachedSnapshot;
    }

    const { undoStack, redoStack } = this.getActiveStacks();
    this.cachedSnapshot = {
      canUndo: undoStack.length > 0,
      canRedo: redoStack.length > 0,
      undoDescription: this.undoDescription,
      redoDescription: this.redoDescription,
      undoCount: undoStack.length,
      redoCount: redoStack.length,
      version: this.version,
      scope: this.currentScope,
    };

    return this.cachedSnapshot;
  }

  private notifyChange(): void {
    this.version++;
    this.cachedSnapshot = null;
    for (const listener of this.listeners) {
      try {
        listener();
      } catch (err) {
        console.error('[CommandManager] Error in listener callback:', err);
      }
    }
  }
}
