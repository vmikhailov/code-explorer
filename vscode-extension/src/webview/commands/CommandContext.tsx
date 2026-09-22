import React, { createContext, useContext, useSyncExternalStore } from 'react';
import { ICommand, ICommandManager, CommandManagerSnapshot } from './types';
import { CommandManager } from './CommandManager';

export interface CommandContextValue {
  commandManager: ICommandManager;
  executeCommand: (command: ICommand) => void;
  undo: () => boolean;
  redo: () => boolean;
  canUndo: boolean;
  canRedo: boolean;
  undoDescription: string | null;
  redoDescription: string | null;
  undoCount: number;
  redoCount: number;
}

const CommandContext = createContext<CommandContextValue | null>(null);

export interface CommandProviderProps {
  manager: CommandManager;
  children: React.ReactNode;
}

export const CommandProvider: React.FC<CommandProviderProps> = ({ manager, children }) => {
  const snapshot: CommandManagerSnapshot = useSyncExternalStore(
    (onStoreChange) => manager.subscribe(onStoreChange),
    () => manager.getSnapshot()
  );

  const value: CommandContextValue = {
    commandManager: manager,
    executeCommand: (command: ICommand) => manager.executeCommand(command),
    undo: () => manager.undo(),
    redo: () => manager.redo(),
    canUndo: snapshot.canUndo,
    canRedo: snapshot.canRedo,
    undoDescription: snapshot.undoDescription,
    redoDescription: snapshot.redoDescription,
    undoCount: snapshot.undoCount,
    redoCount: snapshot.redoCount,
  };

  return <CommandContext.Provider value={value}>{children}</CommandContext.Provider>;
};

export const useCommand = (): CommandContextValue => {
  const context = useContext(CommandContext);
  if (!context) {
    throw new Error('useCommand must be used within a CommandProvider');
  }
  return context;
};
