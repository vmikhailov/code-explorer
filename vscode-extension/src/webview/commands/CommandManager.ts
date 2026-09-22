import { ICommand, ICommandManager, CommandManagerSnapshot } from './types';

export class CommandManager implements ICommandManager {
  private undoStack: ICommand[] = [];
  private redoStack: ICommand[] = [];
  private readonly maxHistorySize: number;
  private listeners: Set<() => void> = new Set();
  private version: number = 0;
  private cachedSnapshot: CommandManagerSnapshot | null = null;

  constructor(maxHistorySize: number = 100) {
    this.maxHistorySize = maxHistorySize;
  }

  public executeCommand(command: ICommand): void {
    try {
      command.execute();
    } catch (err) {
      console.error(`[CommandManager] Error executing command '${command.description}':`, err);
      throw err;
    }

    this.undoStack.push(command);
    if (this.undoStack.length > this.maxHistorySize) {
      this.undoStack.shift();
    }

    // A new action clears the redo stack (standard linear history branching)
    this.redoStack = [];
    this.notifyChange();
  }

  public undo(): boolean {
    if (this.undoStack.length === 0) {
      return false;
    }

    const command = this.undoStack.pop()!;
    try {
      command.undo();
    } catch (err) {
      console.error(`[CommandManager] Error undoing command '${command.description}':`, err);
      // Restore back to undo stack so state isn't permanently corrupted
      this.undoStack.push(command);
      this.notifyChange();
      throw err;
    }

    this.redoStack.push(command);
    this.notifyChange();
    return true;
  }

  public redo(): boolean {
    if (this.redoStack.length === 0) {
      return false;
    }

    const command = this.redoStack.pop()!;
    try {
      command.execute();
    } catch (err) {
      console.error(`[CommandManager] Error redoing command '${command.description}':`, err);
      this.redoStack.push(command);
      this.notifyChange();
      throw err;
    }

    this.undoStack.push(command);
    this.notifyChange();
    return true;
  }

  public get canUndo(): boolean {
    return this.undoStack.length > 0;
  }

  public get canRedo(): boolean {
    return this.redoStack.length > 0;
  }

  public get undoDescription(): string | null {
    return this.undoStack.length > 0 ? this.undoStack[this.undoStack.length - 1].description : null;
  }

  public get redoDescription(): string | null {
    return this.redoStack.length > 0 ? this.redoStack[this.redoStack.length - 1].description : null;
  }

  public clear(): void {
    this.undoStack = [];
    this.redoStack = [];
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

    this.cachedSnapshot = {
      canUndo: this.canUndo,
      canRedo: this.canRedo,
      undoDescription: this.undoDescription,
      redoDescription: this.redoDescription,
      undoCount: this.undoStack.length,
      redoCount: this.redoStack.length,
      version: this.version,
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
