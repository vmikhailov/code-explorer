/**
 * Core interfaces for the Command and Undo/Redo architecture in CodeExplorer Webview.
 */

export interface ICommand {
  /** Unique identifier or category for the command */
  readonly id: string;

  /** Human-readable description displayed in tooltips and logs (e.g. "Switch to Project Flow") */
  readonly description: string;

  /** Executes or re-executes the command */
  execute(): void;

  /** Reverses the command */
  undo(): void;
}

export interface CommandManagerSnapshot {
  readonly canUndo: boolean;
  readonly canRedo: boolean;
  readonly undoDescription: string | null;
  readonly redoDescription: string | null;
  readonly undoCount: number;
  readonly redoCount: number;
  readonly version: number;
  readonly scope?: string;
}

export interface ICommandManager {
  /** Sets the active history scope (e.g. 'flow', 'layers', 'c1') */
  setScope(scope: string): void;

  /** Gets the active history scope */
  getScope(): string;

  /** Executes a command, pushes it to undo stack, and clears redo stack */
  executeCommand(command: ICommand): void;

  /** Reverses the last executed command */
  undo(): boolean;

  /** Re-executes the last undone command */
  redo(): boolean;

  /** Whether an undo operation is available */
  readonly canUndo: boolean;

  /** Whether a redo operation is available */
  readonly canRedo: boolean;

  /** Description of the action that will be undone */
  readonly undoDescription: string | null;

  /** Description of the action that will be redone */
  readonly redoDescription: string | null;

  /** Clears undo and redo history for active scope */
  clear(): void;

  /** Subscribes to history changes */
  subscribe(listener: () => void): () => void;

  /** Gets an immutable snapshot of current manager state */
  getSnapshot(): CommandManagerSnapshot;
}
